<?php
/**
 * E-invoicing page (slug: e-invoicing).
 *
 * Wording follows design-system/BRAND.md: the product produces Phase 2
 * e-invoices; live onboarding with the Authority happens per customer with
 * the customer's own OTP. No "certified/approved" claim.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'الفوترة الإلكترونية (فاتورة)',
		'title'   => 'فواتير إلكترونية وفق متطلبات المرحلة الثانية',
		'lead'    => 'برقوش ERP يولّد الفاتورة، ويوقّعها، ويختمها، ويحفظ تسلسلها — ثم يربط منشأتك بمنصة فاتورة من شاشة الإعدادات.',
	)
);
?>
<section class="section" aria-label="ما يفعله البرنامج">
	<div class="container">
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => brqosh_einvoice_points(), 'cols' => 3 ) ); ?>
	</div>
</section>

<section class="section section-sand" aria-labelledby="onb">
	<div class="container">
		<div class="section-head">
			<p class="eyebrow">خطوات الربط</p>
			<h2 id="onb">كيف تربط منشأتك بمنصة فاتورة</h2>
		</div>
		<ol class="steps" role="list">
			<li class="step"><span class="step-num">1</span><h3>بيانات المنشأة</h3><p>الرقم الضريبي والسجل التجاري والعنوان الوطني في إعدادات المنشأة.</p></li>
			<li class="step"><span class="step-num">2</span><h3>رمز OTP</h3><p>تستخرجه من بوابة فاتورة بحساب منشأتك لدى هيئة الزكاة والضريبة والجمارك.</p></li>
			<li class="step"><span class="step-num">3</span><h3>طلب الشهادة</h3><p>معالج الإعدادات يُنشئ مفتاح المنشأة ويطلب الشهادة (CSID) ويُجري فحوص الامتثال.</p></li>
			<li class="step"><span class="step-num">4</span><h3>الإرسال</h3><p>الفواتير الضريبية تُعتمد والمبسطة تُبلَّغ، وتظهر حالة كل فاتورة في البرنامج.</p></li>
		</ol>
		<p class="note">الربط الحي يتم لكل منشأة بحسابها ورمزها لدى الهيئة. نرافقك في الخطوات، ونتحقق معك من أول فواتير مقبولة.</p>
	</div>
</section>

<section class="section" aria-labelledby="types">
	<div class="container container-narrow">
		<div class="section-head"><h2 id="types">المستندات المدعومة</h2></div>
		<table class="data-table">
			<thead><tr><th>المستند</th><th>النوع</th><th>المسار</th></tr></thead>
			<tbody>
				<tr><td>فاتورة ضريبية</td><td>بين المنشآت (B2B)</td><td>اعتماد (Clearance)</td></tr>
				<tr><td>فاتورة ضريبية مبسطة</td><td>للأفراد ونقاط البيع (B2C)</td><td>إبلاغ (Reporting)</td></tr>
				<tr><td>إشعار دائن</td><td>مرتجع أو تخفيض</td><td>حسب نوع الفاتورة الأصلية</td></tr>
				<tr><td>إشعار مدين</td><td>زيادة على فاتورة</td><td>حسب نوع الفاتورة الأصلية</td></tr>
			</tbody>
		</table>
	</div>
</section>
<?php
get_template_part( 'template-parts/cta' );
get_footer();
