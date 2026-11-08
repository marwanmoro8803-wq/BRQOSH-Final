<?php
/**
 * Marketing content as data: navigation, modules, sectors, FAQ and releases.
 *
 * Every claim here was checked against the product source (src/ in the
 * BRQOSH repository). Wording on e-invoicing follows design-system/BRAND.md:
 * "produces e-invoices per Phase 2 requirements", never "certified".
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

/**
 * Primary navigation used when no WordPress menu is assigned.
 *
 * @return array<int,array{slug:string,label:string}>
 */
function brqosh_nav_items(): array {
	return array(
		array( 'slug' => 'features', 'label' => 'المميزات' ),
		array( 'slug' => 'sectors', 'label' => 'القطاعات' ),
		array( 'slug' => 'e-invoicing', 'label' => 'الفوترة الإلكترونية' ),
		array( 'slug' => 'pricing', 'label' => 'الأسعار' ),
		array( 'slug' => 'downloads', 'label' => 'التحميل' ),
		array( 'slug' => 'contact', 'label' => 'تواصل معنا' ),
	);
}

/**
 * Pages the theme expects. Created on theme activation when missing.
 *
 * @return array<string,string> slug => title
 */
function brqosh_required_pages(): array {
	return array(
		'features'    => 'المميزات',
		'sectors'     => 'القطاعات',
		'e-invoicing' => 'الفوترة الإلكترونية',
		'pricing'     => 'الأسعار',
		'downloads'   => 'التحميل والتحديثات',
		'activation'  => 'التفعيل',
		'company'     => 'من نحن',
		'contact'     => 'تواصل معنا',
		'demo'        => 'جرّبه مجاناً',
		'privacy'     => 'سياسة الخصوصية',
		'terms'       => 'الشروط والأحكام',
		'eula'        => 'اتفاقية الترخيص',
	);
}

/**
 * Core modules (the accounting core and the sections built on it).
 *
 * @return array<int,array{icon:string,title:string,text:string,points:array<int,string>}>
 */
function brqosh_modules(): array {
	return array(
		array(
			'icon'   => 'ledger',
			'title'  => 'الحسابات العامة',
			'text'   => 'دليل حسابات سعودي جاهز، وقيود يومية تمر بالاعتماد ثم الترحيل.',
			'points' => array( 'ميزان مراجعة وأستاذ عام', 'قيود عكسية موثّقة بدل التعديل', 'إقفال الفترات والسنة المالية', 'مراكز تكلفة وعملات متعددة' ),
		),
		array(
			'icon'   => 'invoice',
			'title'  => 'المبيعات والفوترة',
			'text'   => 'عروض أسعار وأوامر بيع وفواتير ضريبية ومبسطة ومرتجعات.',
			'points' => array( 'إشعارات دائنة ومدينة', 'فواتير متكررة', 'سندات تسليم', 'أعمار ديون العملاء' ),
		),
		array(
			'icon'   => 'cart',
			'title'  => 'المشتريات والموردون',
			'text'   => 'أوامر شراء واستلام بضاعة وفواتير موردين بمطابقة ثلاثية.',
			'points' => array( 'مطابقة الأمر والاستلام والفاتورة', 'كشوف حسابات الموردين', 'إشعارات مدينة للموردين', 'أعمار الدائنين' ),
		),
		array(
			'icon'   => 'boxes',
			'title'  => 'المخزون والمستودعات',
			'text'   => 'أصناف بوحدات متعددة ومستودعات وتحويلات وجرد وتكلفة.',
			'points' => array( 'مستودعات متعددة', 'تحويلات بين المستودعات', 'جرد وتسويات', 'تكلفة المخزون في القيد مباشرة' ),
		),
		array(
			'icon'   => 'bank',
			'title'  => 'الخزينة والبنوك',
			'text'   => 'سندات قبض وصرف، وتسوية بنكية من كشف الحساب.',
			'points' => array( 'استيراد كشف البنك CSV', 'مطابقة الحركات', 'مركز النقدية', 'شيكات وتحويلات' ),
		),
		array(
			'icon'   => 'pos',
			'title'  => 'نقاط البيع',
			'text'   => 'بيع سريع بإيصال مطبوع، بورديات وتقارير لكل نقطة.',
			'points' => array( 'جلسات وورديات للكاشير', 'إيصال مطبوع', 'ترحيل فوري للحسابات والمخزون', 'تقارير نقاط البيع' ),
		),
		array(
			'icon'   => 'users',
			'title'  => 'الرواتب والموارد البشرية',
			'text'   => 'مسيرات رواتب وتأمينات اجتماعية ومكافأة نهاية الخدمة.',
			'points' => array( 'تصدير ملف حماية الأجور WPS', 'بدلات واستقطاعات', 'نهاية الخدمة', 'قيد الرواتب تلقائياً' ),
		),
		array(
			'icon'   => 'building',
			'title'  => 'الأصول الثابتة',
			'text'   => 'سجل أصول وإهلاك شهري بمجمّع إهلاك لكل فئة.',
			'points' => array( 'جداول إهلاك', 'استبعاد وبيع الأصول', 'مجمّع لكل فئة', 'قيود الإهلاك آلياً' ),
		),
		array(
			'icon'   => 'chart',
			'title'  => 'التقارير المالية',
			'text'   => 'القوائم المالية كاملة والإقرار الضريبي، مع تصدير PDF.',
			'points' => array( 'قائمة الدخل والمركز المالي', 'التدفقات النقدية', 'التغيرات في حقوق الملكية', 'قوائم موحّدة للفروع والشركات' ),
		),
		array(
			'icon'   => 'factory',
			'title'  => 'التصنيع',
			'text'   => 'قوائم مواد ووصفات وأوامر إنتاج بتكلفة الوحدة.',
			'points' => array( 'قوائم المواد BOM', 'أوامر إنتاج', 'صرف خامات واستلام منتج تام', 'تكلفة الوحدة الفعلية' ),
		),
		array(
			'icon'   => 'crane',
			'title'  => 'المشاريع والمقاولات',
			'text'   => 'مشاريع بتكاليفها ومستخلصاتها وربحيتها.',
			'points' => array( 'مستخلصات وإيراد بنسبة الإنجاز', 'تكاليف المشروع', 'ربحية كل مشروع', 'محتجزات الضمان' ),
		),
		array(
			'icon'   => 'handshake',
			'title'  => 'إدارة العملاء CRM',
			'text'   => 'عملاء محتملون وفرص بيع ومتابعة أنشطة الفريق.',
			'points' => array( 'العملاء المحتملون', 'الفرص ومراحلها', 'الأنشطة والمتابعات', 'خط سير المبيعات' ),
		),
	);
}

/**
 * Sectors served by sector profiles in the setup wizard.
 *
 * @return array<int,array{icon:string,title:string,text:string,points:array<int,string>}>
 */
function brqosh_sectors(): array {
	return array(
		array(
			'icon'   => 'store',
			'title'  => 'الشركات التجارية والتوزيع',
			'text'   => 'مبيعات ومشتريات ومخزون بمستودعات متعددة وفروع.',
			'points' => array( 'تسعير وخصومات', 'مطابقة ثلاثية للمشتريات', 'أعمار ديون', 'تقارير لكل فرع' ),
		),
		array(
			'icon'   => 'factory',
			'title'  => 'المصانع',
			'text'   => 'من قائمة المواد إلى تكلفة الوحدة المنتجة.',
			'points' => array( 'قوائم مواد ووصفات', 'أوامر إنتاج', 'صرف الخامات', 'تكلفة فعلية لكل وحدة' ),
		),
		array(
			'icon'   => 'pos',
			'title'  => 'المتاجر ونقاط البيع',
			'text'   => 'بيع سريع وإيصال وجرد، وكل بيعة في الحسابات فوراً.',
			'points' => array( 'ورديات الكاشير', 'فاتورة مبسطة برمز QR', 'جرد دوري', 'تقارير المبيعات اليومية' ),
		),
		array(
			'icon'   => 'utensils',
			'title'  => 'المطاعم والفنادق',
			'text'   => 'نقاط بيع وصرف مطبخ وحجوزات وفواتير نزلاء.',
			'points' => array( 'صرف المطبخ وتكلفة المكونات', 'حجوزات وعربون', 'ملف النزيل Folio', 'توزيع الإكراميات' ),
		),
		array(
			'icon'   => 'crane',
			'title'  => 'المقاولات والمشاريع',
			'text'   => 'مستخلصات وإيراد بنسبة الإنجاز وربحية كل مشروع.',
			'points' => array( 'مستخلصات العميل', 'تكاليف ومقاولو الباطن', 'محتجزات الضمان', 'ربحية المشروع' ),
		),
		array(
			'icon'   => 'home',
			'title'  => 'العقارات',
			'text'   => 'عقارات ووحدات وعقود إيجار بتحصيلها وتأميناتها.',
			'points' => array( 'سجل العقارات والوحدات', 'عقود الإيجار', 'إثبات الإيراد شهرياً', 'رد التأمين' ),
		),
		array(
			'icon'   => 'stethoscope',
			'title'  => 'العيادات والمراكز الطبية',
			'text'   => 'مرضى وزيارات وصيدلية ومطالبات تأمين.',
			'points' => array( 'ملفات المرضى', 'فوترة الزيارة', 'صرف الصيدلية', 'تسوية مطالبات التأمين' ),
		),
		array(
			'icon'   => 'truck',
			'title'  => 'النقل والتوصيل',
			'text'   => 'رحلات توصيل بتكاليفها وتحصيلها.',
			'points' => array( 'رحلات التوصيل', 'تكلفة الرحلة', 'التحصيل عند التسليم', 'فوترة الشحنات' ),
		),
	);
}

/**
 * The four-step journal lifecycle shown on the home page.
 *
 * @return array<int,array{title:string,text:string}>
 */
function brqosh_lifecycle(): array {
	return array(
		array( 'title' => 'التسجيل', 'text' => 'يُنشأ القيد أو المستند مسودةً قابلة للتعديل.' ),
		array( 'title' => 'الاعتماد', 'text' => 'يراجعه صاحب الصلاحية، والقيود اليدوية فوق حدّ معيّن تحتاج اعتماداً.' ),
		array( 'title' => 'الترحيل', 'text' => 'يُرحَّل إلى الأستاذ العام، ولا يُعدَّل بعده إلا بقيد عكسي موثّق.' ),
		array( 'title' => 'الإقفال', 'text' => 'تُقفل الفترة فيرفض البرنامج أي قيد بتاريخ داخلها.' ),
	);
}

/**
 * Trust and security points (all implemented in src/Brqosh.Infrastructure/Security).
 *
 * @return array<int,array{icon:string,title:string,text:string}>
 */
function brqosh_trust_points(): array {
	return array(
		array( 'icon' => 'lock', 'title' => 'بياناتك على أجهزتك', 'text' => 'قاعدة البيانات على جهازك أو خادم منشأتك (PostgreSQL)، ويعمل البرنامج دون إنترنت.' ),
		array( 'icon' => 'shield', 'title' => 'سجل تدقيق متسلسل', 'text' => 'كل عملية تُسجَّل بمن فعلها ومتى، وسجل التدقيق مربوط بسلسلة تجزئة تكشف أي عبث.' ),
		array( 'icon' => 'key', 'title' => 'صلاحيات وتحقق ثنائي', 'text' => 'أدوار وصلاحيات لكل شاشة، وتحقق بخطوتين (TOTP) وقفل بعد المحاولات الفاشلة.' ),
		array( 'icon' => 'backup', 'title' => 'نسخ احتياطي مجدول', 'text' => 'نسخ تلقائي مجدول، ونسخة أمان قبل كل ترقية، واستعادة من داخل البرنامج.' ),
		array( 'icon' => 'eye', 'title' => 'الخصوصية PDPL', 'text' => 'أدوات للمدير لتصدير بيانات شخص لصاحبها أو إخفاء هويته، مع إثبات في السجل.' ),
		array( 'icon' => 'globe', 'title' => 'عربي وإنجليزي', 'text' => 'الواجهة والتقارير والفواتير باللغتين، مع التاريخ الهجري والميلادي.' ),
	);
}

/**
 * E-invoicing capabilities (src/Brqosh.Infrastructure/Zatca).
 *
 * @return array<int,array{icon:string,title:string,text:string}>
 */
function brqosh_einvoice_points(): array {
	return array(
		array( 'icon' => 'code', 'title' => 'فاتورة بصيغة UBL 2.1', 'text' => 'كل فاتورة ضريبية ومبسطة وإشعار دائن ومدين يُولَّد ملف XML بالصيغة المطلوبة.' ),
		array( 'icon' => 'stamp', 'title' => 'توقيع وختم تشفيري', 'text' => 'توقيع رقمي (ECDSA على المنحنى secp256k1 الذي يطلبه الدليل التقني للهيئة) بمفتاح المنشأة يُثبت سلامة الفاتورة.' ),
		array( 'icon' => 'qr', 'title' => 'رمز QR', 'text' => 'يُطبع على الفاتورة الضريبية والمبسطة بالحقول المطلوبة بترميز TLV.' ),
		array( 'icon' => 'chain', 'title' => 'تسلسل لا ينقطع', 'text' => 'عدّاد الفواتير (ICV) وسلسلة التجزئة (PIH) محميان بقفل حتى مع عدة محطات.' ),
		array( 'icon' => 'server', 'title' => 'موقّع مركزي', 'text' => 'خادم المنشأة يختم فواتير كل المحطات، فلا يتكرر التسلسل ولا ينقطع.' ),
		array( 'icon' => 'wizard', 'title' => 'تسجيل من الإعدادات', 'text' => 'معالج يطلب الشهادة (CSID) ويربط المنشأة ببوابة الهيئة برمز OTP.' ),
	);
}

/**
 * Frequently asked questions. Used on the home and pricing pages and in FAQPage schema.
 *
 * @return array<int,array{q:string,a:string}>
 */
function brqosh_faqs(): array {
	$days  = brqosh_setting( 'trial_days' );
	$price = brqosh_setting( 'price_from' );
	return array(
		array(
			'q' => 'هل يعمل البرنامج دون إنترنت؟',
			'a' => 'نعم. برقوش ERP برنامج سطح مكتب لويندوز، وقاعدة بياناته على جهازك أو خادم منشأتك، فيعمل دون اتصال. يلزم الاتصال فقط عند إرسال الفواتير الإلكترونية إلى الهيئة وعند التفعيل عبر الإنترنت، ويوجد تفعيل دون اتصال.',
		),
		array(
			'q' => 'هل يحسب ضريبة القيمة المضافة؟',
			'a' => 'نعم، بنسبة 15% على كل فاتورة بيع وشراء، ويرحّل الضريبة إلى حساباتها، ويُعدّ ملخص الإقرار الضريبي.',
		),
		array(
			'q' => 'هل البرنامج جاهز للفوترة الإلكترونية (فاتورة)؟',
			'a' => 'يُنتج البرنامج فواتير إلكترونية وفق متطلبات المرحلة الثانية: ملف XML موقّع ورمز QR وتسلسل ICV وPIH. والربط الحي بمنصة فاتورة يتم بتسجيل منشأتك من شاشة الإعدادات برمز OTP من بوابة الهيئة، ونساعدك في خطواته.',
		),
		array(
			'q' => 'هل يعمل على أكثر من جهاز؟',
			'a' => 'نعم. يعمل على جهاز واحد، أو على شبكة المنشأة بقاعدة بيانات PostgreSQL على الخادم يتصل بها عدة مستخدمين وفروع.',
		),
		array(
			'q' => 'هل أستطيع استيراد بياناتي من Excel؟',
			'a' => 'يستورد البرنامج كشوف الحساب البنكية من ملفات CSV لتسويتها، ويُدخل الميزان الافتتاحي دفعة واحدة. ولنقل الأصناف والعملاء والموردين من نظامك السابق نساعدك في تجهيز البيانات.',
		),
		array(
			'q' => 'كم مستخدماً في كل باقة؟',
			'a' => 'الأساسية مستخدمان، والمتقدمة خمسة مستخدمين، والمؤسسات حسب حاجتكم. والأسعار تبدأ من ' . $price . ' ريال حسب المنشأة، والترخيص سنوي أو دائم.',
		),
		array(
			'q' => 'ماذا يحدث بعد انتهاء التجربة؟',
			'a' => 'التجربة ' . $days . ' أيام كاملة بكل الأقسام. بعدها تبقى بياناتك محفوظة على جهازك، وتستمر بعد التفعيل من حيث توقفت دون إعادة إدخال.',
		),
	);
}

/**
 * Public release history (customer-facing summaries, newest first).
 *
 * The newest version must equal 1.1.<version.txt>; website/tools/build-theme.py
 * refuses to package the theme otherwise.
 *
 * @return array<int,array{version:string,date:string,notes:array<int,string>}>
 */
function brqosh_releases(): array {
	return array(
		array(
			'version' => '1.1.3',
			'date'    => '2026-10-28',
			'notes'   => array( 'تصدير ملف حماية الأجور (WPS) بصيغة SIF من صفحة الرواتب للدورة المرحّلة.' ),
		),
		array(
			'version' => '1.1.2',
			'date'    => '2026-10-28',
			'notes'   => array( 'حفظ اتفاقية الترخيص في قاعدة البيانات مع ملف PDF بعد التفعيل.' ),
		),
		array(
			'version' => '1.1.1',
			'date'    => '2026-10-28',
			'notes'   => array( 'تحسين شاشة التفعيل وتسجيل الفوترة الإلكترونية وترجمة رسائلهما.' ),
		),
		array(
			'version' => '1.1.0',
			'date'    => '2026-10-28',
			'notes'   => array(
				'قائمة التغيرات في حقوق الملكية مع تصدير PDF.',
				'أدوات الخصوصية: تصدير بيانات شخص أو إخفاء هويته.',
				'تفعيل دون اتصال مربوط بالجهاز.',
				'موقّع فواتير إلكترونية مركزي على خادم المنشأة.',
				'حسابات جديدة في الدليل ومجمّع إهلاك لكل فئة أصول.',
			),
		),
	);
}
