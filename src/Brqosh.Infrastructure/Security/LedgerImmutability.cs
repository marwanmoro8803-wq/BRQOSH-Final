namespace Brqosh.Infrastructure.Security;

/// <summary>
/// <b>الدفترُ المُرحَّلُ لا يُعدَّل ولا يُحذف من الخادم نفسه</b> (security-licensing-tenancy#5) — على PostgreSQL.
///
/// <para>دورُ التطبيق يكتب الجداولَ، فمن يأخذ سلسلةَ الاتصال من المحطّة كان يستطيع تعديلَ قيدٍ مُرحَّلٍ أو حذفَ سطوره.
/// فالمشغِّلاتُ هنا ترفض على مستوى الخادم: حذفَ قيدٍ غيرِ مسوَّدة، وتعديلَه إلا ما يفعله البرنامجُ نفسُه
/// (مُرحَّل→معكوس، وربطُ قيد العكس، وختمُ فرعٍ لم يُختم)، وتعديلَ سطور قيدٍ غيرِ مسوَّدة أو حذفَها،
/// وتفريغَ الجدولين. والمسوّداتُ حرّة. وسطورُ مسوّدةٍ تُرحَّل في المعاملة نفسها مسموحةٌ بسجلٍّ خاصٍّ
/// لا يكتبه إلا المشغِّل (<c>SECURITY DEFINER</c>)، ولا يملك دورُ التطبيق تعطيلَ المشغِّلات لأنّه ليس مالكَ الجداول.</para>
/// </summary>
public static class LedgerImmutability
{
    /// <summary>جدولُ «رُحِّل في هذه المعاملة» — للمشغِّلات وحدها، لا لدور التطبيق.</summary>
    public const string PostedInTransactionTable = "ledger_posted_in_tx";

    /// <summary>رمزُ الخطأ الذي يرفع به الخادمُ (restrict_violation).</summary>
    public const string RefusalSqlState = "23001";

    public const string InstallSql = """
CREATE TABLE IF NOT EXISTS ledger_posted_in_tx (entry_id bigint NOT NULL, tx bigint NOT NULL, PRIMARY KEY (entry_id, tx));
REVOKE ALL ON ledger_posted_in_tx FROM PUBLIC;
DO $do$ DECLARE g record; BEGIN
    FOR g IN SELECT DISTINCT grantee FROM information_schema.role_table_grants
             WHERE table_name = 'ledger_posted_in_tx' AND grantee <> current_user AND grantee <> 'PUBLIC' LOOP
        EXECUTE format('REVOKE ALL ON ledger_posted_in_tx FROM %I', g.grantee);
    END LOOP;
END $do$;

CREATE OR REPLACE FUNCTION brqosh_guard_journal_entry() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $fn$
BEGIN
    IF OLD."Status" = 'Draft' THEN
        IF TG_OP = 'UPDATE' AND NEW."Status" <> 'Draft' THEN
            DELETE FROM ledger_posted_in_tx WHERE tx <> txid_current();
            INSERT INTO ledger_posted_in_tx VALUES (OLD."Id", txid_current()) ON CONFLICT DO NOTHING;
        END IF;
        RETURN COALESCE(NEW, OLD);
    END IF;
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'قيدٌ مُرحَّل (%) لا يُحذف — يُعكس بقيدٍ معاكس.', OLD."EntryNumber" USING ERRCODE = '23001';
    END IF;
    IF NOT (NEW."Status" = OLD."Status" OR (OLD."Status" = 'Posted' AND NEW."Status" = 'Reversed'))
       OR (OLD."ReversalEntryId" IS NOT NULL AND NEW."ReversalEntryId" IS DISTINCT FROM OLD."ReversalEntryId")
       OR (OLD."BranchId" IS NOT NULL AND NEW."BranchId" IS DISTINCT FROM OLD."BranchId")
       OR (to_jsonb(NEW) - 'Status' - 'ReversalEntryId' - 'BranchId')
          IS DISTINCT FROM (to_jsonb(OLD) - 'Status' - 'ReversalEntryId' - 'BranchId') THEN
        RAISE EXCEPTION 'قيدٌ مُرحَّل (%) لا يُعدَّل — يُعكس بقيدٍ معاكس.', OLD."EntryNumber" USING ERRCODE = '23001';
    END IF;
    RETURN NEW;
END
$fn$;

CREATE OR REPLACE FUNCTION brqosh_guard_journal_line() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $fn$
DECLARE parent_status text;
BEGIN
    SELECT "Status" INTO parent_status FROM journal_entries WHERE "Id" = OLD.journal_entry_id;
    IF parent_status IS NOT NULL AND parent_status <> 'Draft'
       AND NOT EXISTS (SELECT 1 FROM ledger_posted_in_tx WHERE entry_id = OLD.journal_entry_id AND tx = txid_current()) THEN
        RAISE EXCEPTION 'سطرُ قيدٍ مُرحَّل لا يُعدَّل ولا يُحذف — يُعكس القيدُ بقيدٍ معاكس.' USING ERRCODE = '23001';
    END IF;
    RETURN COALESCE(NEW, OLD);
END
$fn$;

CREATE OR REPLACE FUNCTION brqosh_refuse_ledger_truncate() RETURNS trigger LANGUAGE plpgsql AS $fn$
BEGIN
    RAISE EXCEPTION 'جداولُ الدفتر لا تُفرَّغ.' USING ERRCODE = '23001';
END
$fn$;

DROP TRIGGER IF EXISTS brqosh_guard_journal_entry ON journal_entries;
CREATE TRIGGER brqosh_guard_journal_entry BEFORE UPDATE OR DELETE ON journal_entries
    FOR EACH ROW EXECUTE FUNCTION brqosh_guard_journal_entry();
DROP TRIGGER IF EXISTS brqosh_guard_journal_line ON journal_entry_lines;
CREATE TRIGGER brqosh_guard_journal_line BEFORE UPDATE OR DELETE ON journal_entry_lines
    FOR EACH ROW EXECUTE FUNCTION brqosh_guard_journal_line();
DROP TRIGGER IF EXISTS brqosh_refuse_truncate ON journal_entries;
CREATE TRIGGER brqosh_refuse_truncate BEFORE TRUNCATE ON journal_entries
    FOR EACH STATEMENT EXECUTE FUNCTION brqosh_refuse_ledger_truncate();
DROP TRIGGER IF EXISTS brqosh_refuse_truncate ON journal_entry_lines;
CREATE TRIGGER brqosh_refuse_truncate BEFORE TRUNCATE ON journal_entry_lines
    FOR EACH STATEMENT EXECUTE FUNCTION brqosh_refuse_ledger_truncate();
""";

    public const string UninstallSql = """
DROP TRIGGER IF EXISTS brqosh_refuse_truncate ON journal_entry_lines;
DROP TRIGGER IF EXISTS brqosh_refuse_truncate ON journal_entries;
DROP TRIGGER IF EXISTS brqosh_guard_journal_line ON journal_entry_lines;
DROP TRIGGER IF EXISTS brqosh_guard_journal_entry ON journal_entries;
DROP FUNCTION IF EXISTS brqosh_refuse_ledger_truncate();
DROP FUNCTION IF EXISTS brqosh_guard_journal_line();
DROP FUNCTION IF EXISTS brqosh_guard_journal_entry();
DROP TABLE IF EXISTS ledger_posted_in_tx;
""";
}
