<?php
/**
 * Downloads and changelog (slug: downloads).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
$brqosh_url = brqosh_setting( 'download_url' );
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'التحميل والتحديثات',
		'title'   => 'تحميل برقوش ERP',
		'lead'    => 'آخر إصدار مع بصمته وسجل التحديثات. والتحديثات اللاحقة يتحقق البرنامج من بيانها الموقّع وبصمة حزمتها قبل تنصيبها، مع نسخة أمان تلقائية قبل كل ترقية.',
	)
);
?>
<section class="section" aria-labelledby="latest">
	<div class="container release">
		<div class="release-card">
			<h2 id="latest">آخر إصدار</h2>
			<dl class="release-meta">
				<div><dt>الإصدار</dt><dd><bdi><?php echo esc_html( brqosh_setting( 'release_version' ) ); ?></bdi></dd></div>
				<div><dt>تاريخ الإصدار</dt><dd><bdi><?php echo esc_html( brqosh_setting( 'release_date' ) ); ?></bdi></dd></div>
				<div><dt>الحجم</dt><dd><bdi><?php echo esc_html( brqosh_setting( 'release_size' ) ); ?></bdi></dd></div>
			</dl>
			<div class="hash">
				<span>SHA-256</span>
				<code dir="ltr" data-copy-source><?php echo esc_html( brqosh_setting( 'release_sha256' ) ); ?></code>
				<button class="btn btn-ghost btn-sm" type="button" data-copy>نسخ</button>
			</div>
			<?php if ( '' !== $brqosh_url ) : ?>
				<a class="btn btn-primary btn-lg" href="<?php echo esc_url( $brqosh_url ); ?>"><?php echo brqosh_icon( 'download' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> تحميل المُنصِّب</a>
			<?php else : ?>
				<a class="btn btn-primary btn-lg" href="<?php echo brqosh_page_url( 'demo' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>"><?php echo brqosh_icon( 'download' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> اطلب رابط التحميل</a>
				<p class="note">نرسل رابط التحميل بعد التسجيل لضمان حصولك على النسخة الصحيحة والدعم في التنصيب.</p>
			<?php endif; ?>
			<p class="note">تحقّق من البصمة قبل التشغيل في PowerShell:</p>
			<pre dir="ltr"><code><?php echo esc_html( 'Get-FileHash .\\BRQOSH-Setup-' . brqosh_setting( 'release_version' ) . '.msi -Algorithm SHA256' ); ?></code></pre>
		</div>
		<div>
			<h2>متطلبات التشغيل</h2>
			<ul class="ticks" role="list">
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>Windows 10 أو 11 (64-بت)، أو Windows Server 2016 فأحدث</li>
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>للشبكة: خادم PostgreSQL للمنشأة (نساعدك في إعداده)</li>
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>إنترنت للفوترة الإلكترونية والتفعيل عبر الإنترنت فقط</li>
			</ul>
			<h2>خطوات التنصيب</h2>
			<ol class="steps steps-compact" role="list">
				<li class="step"><span class="step-num">1</span><h3>حمّل المُنصِّب</h3><p>من الرابط المرسل إليك، وتحقق من البصمة.</p></li>
				<li class="step"><span class="step-num">2</span><h3>شغّل المُنصِّب</h3><p>وافق على اتفاقية الترخيص واختر مجلد التنصيب.</p></li>
				<li class="step"><span class="step-num">3</span><h3>أنشئ المنشأة</h3><p>معالج الإعداد يطلب بيانات المنشأة والرقم الضريبي ونوع النشاط.</p></li>
				<li class="step"><span class="step-num">4</span><h3>فعّل الترخيص</h3><p>تبدأ التجربة تلقائياً، وفعّل متى شئت من <a href="<?php echo brqosh_page_url( 'activation' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">صفحة التفعيل</a>.</p></li>
			</ol>
		</div>
	</div>
</section>

<section class="section section-sand" id="changelog" aria-labelledby="log">
	<div class="container container-narrow">
		<div class="section-head"><h2 id="log">سجل التحديثات</h2></div>
		<ol class="changelog" role="list">
			<?php foreach ( brqosh_releases() as $brqosh_rel ) : ?>
				<li>
					<div class="changelog-head"><strong><bdi><?php echo esc_html( $brqosh_rel['version'] ); ?></bdi></strong><time datetime="<?php echo esc_attr( $brqosh_rel['date'] ); ?>"><?php echo esc_html( $brqosh_rel['date'] ); ?></time></div>
					<ul role="list">
						<?php foreach ( $brqosh_rel['notes'] as $brqosh_note ) : ?>
							<li><?php echo esc_html( $brqosh_note ); ?></li>
						<?php endforeach; ?>
					</ul>
				</li>
			<?php endforeach; ?>
		</ol>
	</div>
</section>
<?php
get_template_part( 'template-parts/cta' );
get_footer();
