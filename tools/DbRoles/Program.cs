using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

// (ح-١٥١ · security-licensing-tenancy#5) ‏أداةُ المسؤول: دورُ التطبيق بأقلّ صلاحيّة، والترقيةُ باتّصال المالك.
// ‏**والاعتماداتُ من متغيّرات البيئة لا من سطر الأوامر** — فلا تبقى في سجلّ الطرفيّة ولا في قائمة العمليّات، ولا تُطبع أبداً.

const string OwnerVariable = "BRQOSH_OWNER_CONNECTION";
const string PasswordVariable = "BRQOSH_APP_ROLE_PASSWORD";

if (args.Length == 0)
    return Usage();

var owner = Environment.GetEnvironmentVariable(OwnerVariable);
if (string.IsNullOrWhiteSpace(owner))
{
    Console.Error.WriteLine($"اضبط {OwnerVariable} بسلسلة اتصال مالك القاعدة (PostgreSQL).");
    return 2;
}

try
{
    switch (args[0])
    {
        case "create-app-role" when args.Length >= 2:
        {
            var password = Environment.GetEnvironmentVariable(PasswordVariable);
            if (string.IsNullOrEmpty(password))
            {
                Console.Error.WriteLine($"اضبط {PasswordVariable} بكلمة مرور الدور ({LeastPrivilegeDatabaseRole.MinimumPasswordLength} محرفاً على الأقلّ).");
                return 2;
            }

            await LeastPrivilegeDatabaseRole.ApplyAsync(owner, args[1], password);
            Console.WriteLine($"الدور {args[1]}: قراءةٌ وكتابةٌ على الجداول، بلا تعريفٍ للمخطّط، وسجلُّ التدقيق إضافةٌ فقط.");
            Console.WriteLine("اكتب في ملفّ اتصال كلّ محطّةٍ هذا الدورَ وكلمةَ مروره — لا اعتمادَ المالك.");
            return 0;
        }

        case "upgrade":
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, owner);
            await new DatabaseSchemaService(db).EnsureSchemaAsync();
            await new BrqoshSchemaUpdater(db).ApplyAsync();
            Console.WriteLine("تمّت ترقيةُ المخطّط باتّصال المالك — المحطّاتُ تُقلع بدورها بعدها.");
            return 0;
        }

        default:
            return Usage();
    }
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Npgsql.NpgsqlException)
{
    Console.Error.WriteLine(ex.Message);
    return 3;
}

static int Usage()
{
    Console.WriteLine("""
        BRQOSH DbRoles
        --------------
        create-app-role <role>   — دورُ التطبيق بأقلّ صلاحيّة (يُعاد بلا ضرر لتغيير كلمة المرور)
                                   BRQOSH_OWNER_CONNECTION · BRQOSH_APP_ROLE_PASSWORD
        upgrade                  — ترقيةُ المخطّط باتّصال المالك (المحطّاتُ بالدور لا تُعدّل المخطّط)
                                   BRQOSH_OWNER_CONNECTION
        """);
    return 1;
}
