<?php
/**
 * Home page.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
$brqosh_days = brqosh_setting( 'trial_days' );
?>
<section class="hero">
	<div class="hero-bg" aria-hidden="true"><?php echo brqosh_svg_file( 'mark' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></div>
	<div class="container hero-grid">
		<div class="hero-copy">
			<p class="pill"><?php echo brqosh_icon( 'shield' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> فواتير إلكترونية وفق متطلبات المرحلة الثانية · ضريبة 15%</p>
			<h1>حساباتك وفواتيرك ومخزونك<br><span class="accent">في برنامج واحد يفهم السوق السعودي</span></h1>
			<p class="lead">برقوش ERP برنامج محاسبة عربي لويندوز: قيود تمر بالاعتماد والترحيل، فواتير ضريبية بختم ورمز QR، مخزون وفروع ونقاط بيع ورواتب — وبياناتك على أجهزتك لا على خوادم غيرك.</p>
			<div class="hero-actions">
				<a class="btn btn-primary btn-lg" href="<?php echo brqosh_page_url( 'demo' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">جرّبه مجاناً <?php echo esc_html( $brqosh_days ); ?> أيام</a>
				<a class="btn btn-ghost btn-lg" href="<?php echo brqosh_page_url( 'features' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">استعرض المميزات <?php echo brqosh_icon( 'arrow' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></a>
			</div>
			<ul class="hero-meta" role="list">
				<li><?php echo brqosh_icon( 'windows' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> ويندوز 10 و11</li>
				<li><?php echo brqosh_icon( 'globe' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> عربي وإنجليزي</li>
				<li><?php echo brqosh_icon( 'download' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> الإصدار <bdi><?php echo esc_html( brqosh_setting( 'release_version' ) ); ?></bdi></li>
			</ul>
		</div>

		<div class="hero-visual" aria-label="مثال: فاتورة ضريبية وقيدها المرحّل تلقائياً">
			<div class="app-window">
				<div class="app-bar"><span></span><span></span><span></span><b>برقوش ERP — فاتورة مبيعات</b></div>
				<div class="app-body">
					<div class="inv-head">
						<div><small>فاتورة ضريبية</small><strong dir="ltr">INV-2026-0148</strong></div>
						<span class="badge badge-ok"><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> مرحّلة</span>
					</div>
					<table class="mini-table">
						<thead><tr><th>البيان</th><th>الكمية</th><th>المبلغ</th></tr></thead>
						<tbody><tr><td>خدمات توريد وتركيب</td><td class="num">1</td><td class="num">10,000.00</td></tr></tbody>
						<tfoot>
							<tr><td colspan="2">المجموع قبل الضريبة</td><td class="num">10,000.00</td></tr>
							<tr><td colspan="2">ضريبة القيمة المضافة 15%</td><td class="num">1,500.00</td></tr>
							<tr class="total"><td colspan="2">الإجمالي (ر.س)</td><td class="num">11,500.00</td></tr>
						</tfoot>
					</table>
					<div class="inv-qr">
						<span class="qr-art" aria-hidden="true"></span>
						<p>ختم إلكتروني ورمز QR يُضافان عند الاعتماد.<br><small dir="ltr">ICV 148 · PIH ✓</small></p>
					</div>
				</div>
			</div>
			<div class="entry-card">
				<p class="entry-title"><?php echo brqosh_icon( 'ledger' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> القيد المرحّل تلقائياً</p>
				<table class="mini-table">
					<thead><tr><th>الحساب</th><th>مدين</th><th>دائن</th></tr></thead>
					<tbody>
						<tr><td>العملاء</td><td class="num">11,500</td><td class="num muted">—</td></tr>
						<tr><td>إيرادات المبيعات</td><td class="num muted">—</td><td class="num">10,000</td></tr>
						<tr><td>ضريبة مستحقة</td><td class="num muted">—</td><td class="num">1,500</td></tr>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</section>

<section class="trust-strip" aria-label="أبرز الضمانات">
	<div class="container">
		<ul role="list">
			<li><?php echo brqosh_icon( 'lock' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>بياناتك عندك</b>على جهازك أو خادمك</span></li>
			<li><?php echo brqosh_icon( 'qr' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>فاتورة إلكترونية</b>XML موقّع ورمز QR</span></li>
			<li><?php echo brqosh_icon( 'shield' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>سجل تدقيق</b>لا يُحذف قيد مرحّل</span></li>
			<li><?php echo brqosh_icon( 'clock' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>يعمل دون إنترنت</b>ويتصل عند الإرسال فقط</span></li>
		</ul>
	</div>
</section>

<section class="section" aria-labelledby="modules-title">
	<div class="container">
		<div class="section-head">
			<p class="eyebrow">الأقسام</p>
			<h2 id="modules-title">نواة محاسبية واحدة، وكل قسم يرحّل أثره إليها</h2>
			<p>لا تُدخل الرقم مرتين: الفاتورة والاستلام والراتب والإهلاك كلها تصل إلى الأستاذ العام بقيد متوازن.</p>
		</div>
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => array_slice( brqosh_modules(), 0, 8 ), 'numbered' => true, 'cols' => 4 ) ); ?>
		<p class="section-more"><a class="link-arrow" href="<?php echo brqosh_page_url( 'features' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">كل الأقسام والمميزات <?php echo brqosh_icon( 'arrow' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></a></p>
	</div>
</section>

<section class="section section-sand" aria-labelledby="cycle-title">
	<div class="container">
		<div class="section-head">
			<p class="eyebrow">دورة القيد</p>
			<h2 id="cycle-title">لا رقم يدخل الدفاتر دون مراجعة</h2>
			<p>رقابة داخلية مبنية في البرنامج، لا تعتمد على حسن النية.</p>
		</div>
		<ol class="steps" role="list">
			<?php foreach ( brqosh_lifecycle() as $brqosh_n => $brqosh_step ) : ?>
				<li class="step reveal">
					<span class="step-num"><?php echo esc_html( (string) ( $brqosh_n + 1 ) ); ?></span>
					<h3><?php echo esc_html( $brqosh_step['title'] ); ?></h3>
					<p><?php echo esc_html( $brqosh_step['text'] ); ?></p>
				</li>
			<?php endforeach; ?>
		</ol>
	</div>
</section>

<section class="section" aria-labelledby="sectors-title">
	<div class="container">
		<div class="section-head">
			<p class="eyebrow">القطاعات</p>
			<h2 id="sectors-title">يبدأ بنشاطك، ويفعّل ما يلزمه</h2>
			<p>تختار نوع النشاط عند إنشاء المنشأة، فيفعّل البرنامج الأقسام المناسبة ويُخفي ما لا تحتاجه.</p>
		</div>
		<ul class="sector-chips" role="list">
			<?php foreach ( brqosh_sectors() as $brqosh_sector ) : ?>
				<li class="reveal"><a href="<?php echo brqosh_page_url( 'sectors' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>"><?php echo brqosh_icon( $brqosh_sector['icon'] ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b><?php echo esc_html( $brqosh_sector['title'] ); ?></b><?php echo esc_html( $brqosh_sector['text'] ); ?></span></a></li>
			<?php endforeach; ?>
		</ul>
	</div>
</section>

<section class="section section-dark" aria-labelledby="trust-title">
	<div class="container">
		<div class="section-head">
			<p class="eyebrow">الأمان والملكية</p>
			<h2 id="trust-title">دفاترك ملكك، ومحمية كما يجب</h2>
		</div>
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => brqosh_trust_points(), 'cols' => 3 ) ); ?>
	</div>
</section>

<section class="section" aria-labelledby="price-title">
	<div class="container price-teaser">
		<div>
			<p class="eyebrow">الأسعار</p>
			<h2 id="price-title">تبدأ من <bdi><?php echo esc_html( brqosh_setting( 'price_from' ) ); ?></bdi> ريال</h2>
			<p>سعر واضح حسب حجم منشأتك وعدد المستخدمين والفروع، بترخيص سنوي أو دائم. والسعر النهائي مكتوب في عرض السعر قبل أي التزام.</p>
		</div>
		<div class="price-teaser-actions">
			<a class="btn btn-primary" href="<?php echo brqosh_page_url( 'pricing' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">قارن الباقات</a>
			<a class="btn btn-ghost" href="<?php echo brqosh_page_url( 'contact' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">اطلب عرض سعر</a>
		</div>
	</div>
</section>

<?php
get_template_part( 'template-parts/faq' );
get_template_part( 'template-parts/cta' );
get_footer();
