# Attendance Legal Applicability Research - 12 October 2026

Status: research for independent review; financial legal activation BLOCKED.

## Second-pass retrieval note

On the second local continuation, official-host search again returned the
[Sindh wage Act](https://sindhlaws.gov.pk/setup/publications_SindhCode/PUB-NEW-18-000186.pdf)
and the [KP wage Act](https://www.kpcode.kp.gov.pk/uploads/2013_9_THE_KHYBER_PAKHTUNKHWA_PAYMENT_OF_WAGES_ACT_20131.pdf).
The [Pakistan Code wage-law record](https://pakistancode.gov.pk/english/index.php/UY2FqaJw1-apaUY2Fqa-b56b-sg-jjjjjjjjjjjjj)
was available, but the PDF tool produced inconsistent title/retrieval metadata for its linked PDF.
The Punjab Labour Code PDF and Balochistan wage PDF could not be opened by that tool in this pass.
These are retrieval limits, not proof that a law is absent, repealed or in force. No new commencement,
notification, worker classification, statutory amount or legal approval was inferred. The original
source-specific evidence and unresolved applicability questions below remain subject to counsel review.

This document records primary-source findings and engineering recommendations. It is not legal advice, a legal opinion, certification of the company formula, an appointment of a reviewer, or approval to deduct, withhold, post or release wages. Main implementation owns any subsequent product changes. This sidecar creates only this document; it performs no product/shared-document edits, migrations, SQL or Git mutations.

## 1. Local evidence and authority boundary

Read `AGENTS.md`, `README.md`, `docs/Architecture.md`, `docs/SRS-Module-Map.md`, the HR SRS v1.2 DOCX, and the current attendance completion records: `Continuation-20261012.md`, `Approved-Workflow-Continuation.md`, `Review-20261012.md`, `Policy-Integration-Contract.md`, `Policy-Leave-Implementation.md`, `Payroll-Inputs-Implementation.md`, `Overtime.md`, and `Company-Settings.md`. The DOCX was read using its ZIP `word/document.xml`, with namespace-aware XML paragraph extraction; sections 8, 9 and 19 were specifically rechecked.

Relevant local baseline:

- SRS sections 8.1/8.2 describe nine-hour, six-day schedules, three-lates/one-day deductions, half-day treatment, paired-worker waiting loss and KPI effects. Sections 8.3/19 originally leave OT rates and several approvals/calculation details to management confirmation. The SRS's account of signed September policies does not supply the actual signed documents or establish statutory compliance.
- Current OT documentation records monthly BASIC / actual source-month calendar days / 9 * approved hours, whole-PKR final rounding, elapsed duty without deducting configured breaks for that company formula, and company eligibility excluding office staff. These are company calculation choices, not statutory entitlements or evidence of lawful exclusion.
- Current payroll-input documentation records basic/month-days ordinary candidates, configurable late candidates, larger-only overlap suppression and two-decimal rounding. A preview or immutable input revision is not authority to collect money.
- Company settings use explicit dated site mappings for Punjab, Sindh, KP, Balochistan and ICT; their internal codes are labels, not law classifications. `legalStatus` remains `review_required`; missing location must not inherit Head Office's jurisdiction.

The user PROPOSED, but has NOT appointed or formally authorized, the following future clearance sequence: legal counsel signs a clearance; HR supplies classified location/worker/establishment evidence; SuperAdmin performs final activation. No counsel identity, appointment/delegation instrument, approved clearance scope, HR classification authority or SuperAdmin legal activation authority has been established here. Existing technical SuperAdmin/configuration rights do not establish any of those powers. ACTIVATION must remain blocked until formal authority AND evidence are confirmed. Counsel's internal clearance also cannot replace a government/prescribed-authority approval required by an applicable enactment.

## 2. Research method and currency limits

Research date: 2026-10-12, Asia/Karachi. Sources below are official legislation portals, provincial assemblies, government departments or the state broadcaster. Secondary search hits were used only to locate official documents; they are not authority for conclusions. No long statutory passage is reproduced.

Web retrieval intermittently timed out for Punjab, KP and ICT. Successful read-only HTTPS requests and in-memory `pypdf` extraction verified Punjab/KP PDFs and ICT department content. No downloaded source or extraction file was written. Balochistan's wage PDF was image-only to the local extractor; its official-host indexed text verified the identified sections. That is weaker than visual comparison against every gazette page and is an express evidence gap. The scanned 2022 shops amendment on the government gateway was cross-checked against the Assembly's extractable version.

An official-host URL verifies provenance of the retrieved document, not a complete amendment history through this research date. A search result's crawl/publication date or a `/uploads/` directory date is not enactment or commencement. Searches did not establish a complete 2026 gazette/notification/court-order chain for any jurisdiction. Absence of a located amendment is not proof that none exists. Obtain authenticated current text, applicable rules, notifications, exemptions and judicial interpretations before activation, and resolve coverage as of each duty date.

Currency has two separate meanings here: legal currency remains subject to that verification; monetary outputs currently support PKR in the local company formula. Neither a historical rupee/anna expression nor a current PKR output validates a rate, payment channel or deduction. No current minimum-wage amount, wage-coverage ceiling, contribution rate, tax rate or exchange rate is certified by this research.

## 3. Punjab: commencement is a material unresolved dependency

Official discovery: [Punjab alphabetical legislation index](https://www.punjablaws.gov.pk/chronalphabettable.php) links to the following PDFs. Older `/laws/*.html` URLs failed in this session; the PDFs were successfully retrieved directly.

### PB-1: Punjab Labour Code 2026, Act IX of 2026

[Official Code PDF](https://www.punjablaws.gov.pk/lawspdf/3005.pdf), sections 1(3), 2-5, 164, 168-170, 175-176, 179, 186-187, 434-435; relevant PDF pages 14-16, 85-91, 170-171.

The publication footnote records passage 4 February, assent/publication 10 February 2026. Section 1(3) requires Gazette-notified commencement. Sections 2-5 broadly address workplaces/workers/employees, including indirect engagements; chapter-specific coverage still matters. Sections 168-170 distinguish deductions/fines/unjustified absence. Section 169 requires authorization through the Code, collective agreement or internal regulations, notice/show-cause, a 3% remuneration cap, no fines below 18, no instalments, 120-day recovery limit and a beneficial-purpose register. Section 170 uses a proportional maximum referencing the total wage period: counsel must resolve its denominator, rather than importing old section 9 unchanged. Sections 176/179 generally specify 8 daily/48 weekly hours, consent/limits and 2x ordinary/rest-day or 3x public-holiday OT, subject to stated qualifications/substitute-rest provisions. Sections 186-187/434 require specific exemption/reduction evidence. Section 435 repeals listed predecessors and preserves qualifying prior instruments/proceedings.

[Radio Pakistan, 8 October 2026](https://www.radio.gov.pk/08-10-2026/special-rehabilitation-centres-being-set-up-to-end-child-labour-cm-punjab) reports the Chief Minister describing the Code as enforced. This official statement supports investigating commencement urgently; it does not provide the Gazette notification number, operative date, transitional interpretation or Cosmix-specific coverage. That notification was NOT verified here. Do not treat enactment date as commencement or assume the predecessors continue unchanged.

### PB-2: predecessor texts, for historical/transition review only

[Punjab Payment of Wages Act 1936 PDF](https://www.punjablaws.gov.pk/lawspdf/45a.pdf), sections 1-2, 7, 8(1)-(8), 9(1)-(2), 23; PDF pages 6-8 contain the deduction/fine distinction. Section 8's official footnote attributes the 3% substitution to Punjab Act VII of 2014. It requires prior government/prescribed-authority approval, notice, show-cause, register/approved beneficial use, no instalments and a 60-day limit. Section 9 bounds ordinary absence deductions by absent time divided by required work time in the wage period. Its concerted-absence proviso is exceptional, not a routine late penalty.

[Punjab Shops and Establishments Ordinance 1969 PDF](https://www.punjablaws.gov.pk/lawspdf/230.pdf), sections 1-2, 4-5, 6-10, 21; PDF pages 8-9 show adult 9 daily/48 weekly hours, rest requirements, qualified OT at double ordinary wages and a contract piece-rate proviso. Footnotes incorporate Act VI of 2023. Sections 2/4/5 require establishment and exemption review; office/contractor/engineer coverage cannot be reduced to the ERP's `OFFICE` label. These predecessor findings are conditional on verified transition dates under PB-1, not a current-law declaration.

## 4. Sindh

### SD-1: Payment of Wages Act 2015, Act VI of 2017

[Official wage Act PDF](https://sindhlaws.gov.pk/setup/publications_SindhCode/PUB-NEW-18-000186.pdf), sections 1, 2(1)(b)-(d)/(g)/(m), 3, 7, 8(1)-(7), 9(1)-(2), 23, 29; PDF pages 2-8.

The header is dated 22 March 2017 despite the short title's 2015 year; section 1(3) says immediate commencement. Definitions include contractor establishments/sites, clerical work and contract workers; the employed-person definition excludes an occupier/manager with hiring/firing authority. Actual duties and powers require evidence. Section 7 restricts deduction types. Section 8 requires prior government/prescribed-authority approval, displayed notice, opportunity to show cause (the text refers to thirty days), a 3% wage-period cap, no instalments/collection after 60 days, and registered beneficial-purpose use. Section 9's ordinary absence maximum is proportional to required work time. Section 2(m)'s wage definition is broader than simply BASIC. Current rules, amendments and application to each employee remain unverified.

### SD-2: Shops and Commercial Establishment Act 2015, Act XII of 2016

[Official shops Act PDF](https://sindhlaws.gov.pk/setup/publications_SindhCode/PUB-NEW-18-000109.pdf), sections 1(3)-(4), 2(f)/(h)/(o)/(p), 4-5, 6-10, 21, 33-34; PDF pages 2-9.

Header date: 29 April 2016. Immediate application is tied to previously covered areas/classes, with notification powers. Commercial coverage expressly includes contractor/engineer offices; repair workshops are industrial-establishment candidates. Sections 4/5 address exemptions, including particular persons such as managers/travelling agents: no automatic exclusion of all office employees. Adult limits are 9 daily/48 weekly hours; section 8's printed adult annual OT limit is 150 hours. Section 9 prints an awkward double-rate phrase, which should be checked against the Gazette. Section 2(o) excludes rest/meals from working hours; section 7(3) requires rest, section 6 protects weekly-holiday wages, and section 10 has seasonal spread-over limits. These qualifiers require review, not a universal nine-hour rule.

### SD-3: alternative factory coverage

[Sindh Factories Act 2015, Act XIII of 2016](https://www.sindhlaws.gov.pk/setup/publications_SindhCode/PUB-NEW-18-000110.pdf), sections 2, 54, 57-59, 64-68; PDF pages 35 and 41. Section 57 describes nine daily hours including breaks; section 68 addresses double ordinary pay, with non-seasonal 9 daily/48 weekly and seasonal 10 daily/50 weekly thresholds. The ordinary-pay definition has inclusions/exclusions. Determine whether an HVAC workshop or work connected with a manufacturing process is covered before choosing this track. Do not transplant shops working-time semantics into a factory calculation. Full current factory coverage and amendment reconciliation remain outstanding.

## 5. Khyber Pakhtunkhwa

### KP-1: Payment of Wages Act 2013, Act IX of 2013

[Official wage Act PDF](https://www.kpcode.kp.gov.pk/uploads/2013_9_THE_KHYBER_PAKHTUNKHWA_PAYMENT_OF_WAGES_ACT_20131.pdf), sections 1(3)-(4), 2(ii)/(vi)(g)/(xiv), 3, 7-9, 23, 28; PDF pages 3-10.

Publication: 1 February 2013; commencement is immediate. Coverage expressly includes contractor/subcontractor engagements and contractor sites. The wage definition includes basic pay and statutory/non-statutory allowances with exclusions. Sections 7/9 restrict ordinary absence deductions proportionately to required work time. Section 8 requires prior government/prescribed-authority approval, notice/show-cause, a 3% wage-period cap, a 60-day collection limit, and registered approved beneficial use. The retrieved consolidation contains amendment footnotes, including Act XVIII of 2015; it is not evidence of an exhaustive amendment search. Read the applicable rules separately before any fine workflow.

### KP-2: Shops and Establishments Act 2015, Act XX of 2015

[Official consolidated shops Act PDF](https://kpcode.kp.gov.pk/uploads/THE_KHYBER_PAKHTUNKHWA_SHOPS_AND_ESTABLISHMENT_ACT_2015.pdf), sections 1-2, 4-10, 22, 35, 38; PDF pages 3-9.

The PDF states first publication 12 May 2015 and immediate commencement; the [Assembly record](https://www.pakp.gov.pk/act/the-khyber-pakhtunkhwa-shops-and-establishments-act-2015/) lists enforcement 11 May. Reconcile that discrepancy for historical selection. Sections 2(g)/(i)/(p)/(q)/(aa) cover contractor/engineer offices, employee/workshop definitions, rest-excluded working hours and cash wage components. The consolidation references Act XIX of 2021. Section 8 specifies adult 8 daily/48 weekly hours, with qualified OT and a printed 24-hour weekly OT limit; section 9 specifies double ordinary wages. Sections 4/5 require actual exemption review. KP shops cannot inherit Punjab's predecessor nine-hour trigger. A company OT eligibility toggle is not proof of statutory exclusion.

### KP-3: alternative factory coverage

[Official consolidated Factories Act 2013 PDF](https://kpcode.kp.gov.pk/uploads/THE_KHYBER_PAKHTUNKHWA_FACTORIES_ACT_2013.pdf), sections 2, 66-67; section 67 is on PDF page 32. Section 67 provides double ordinary wage beyond non-seasonal 9 daily/48 weekly or seasonal 9 daily/50 weekly limits, with government-fixed equivalent time rates for piece workers. The copy has amendment footnotes, so use the correct consolidation. The [Assembly original](https://www.pakp.gov.pk/wp-content/uploads/2024/04/The-Khyber-Pakhtunkhwa-Factories-Act-2013-Act-No-XVI-2013.pdf) records assent 16 March and publication 19 March 2013; another official copy reports 20 March. Resolve original/consolidated dates and applicable amendments. Factory and shops thresholds differ within KP; activity, premises and worker evidence are essential.

## 6. Balochistan

### BA-1: Payment of Wages Act 2021, Act XIII of 2021

[Assembly wage Act PDF](https://pabalochistan.gov.pk/storage/4221/2021-11-08_11_54_47_90e3f.pdf), sections 1(3)-(5), 2, 8, 9(1)-(8), 10(1)-(2); printed pages 2, 6-8.

The indexed official header records notification 5 May 2021; section 1(3) states immediate commencement. Exact assent/publication sequence remains unverified. Section 1(4) broadly includes contractor establishments and other specified sectors. Section 8 permits listed deduction types. Section 9 requires prior government/prescribed-authority approval, notice/show-cause, a 1% wage-period fine cap, protection for adolescent/young workers, no instalments/collection after 60 days and registered approved beneficial use. Section 10 bounds ordinary absence deduction by absent/required work time; its concerted-absence proviso differs from the eight-day wording elsewhere. Detailed current rules, definitions, subsequent amendments and scan/Gazette comparison remain evidence gaps. Do not copy Sindh/KP's 3% cap or section numbering into this jurisdiction.

### BA-2: Shops and Establishments Act 2021, Act XVII of 2021

[Official government shops Act PDF](https://balochistan.gov.pk/wp-content/uploads/2024/10/The-Balochistan-Shops-and-Establishments-Act-2021.pdf), sections 1-2, 4-10, 25, 28; PDF pages 1, 7-11. Header: passed 31 May, assent 11 June, notification 14 June 2021. Section 1 links immediate application to previously covered areas/classes and permits notification changes. Sections 2/4/5 require office/workshop/worker and exemption classification. Sections 7-10 address rest, adult 9 daily/48 weekly limits, qualified OT at double ordinary wages, a contract piece-rate proviso and spread-over. The [2022 amendment, Act XV](https://pabalochistan.gov.pk/storage/4264/2022-06-14_08_41_17_d121e.pdf), sections 1-3, records assent 28 April/notification 29 April 2022 and immediate commencement; it changes registration-heading/Schedule details, not those hour/rate sections. Later changes were not exhaustively excluded.

### BA-3: alternative factory coverage

[Assembly Factories Act 2021 PDF](https://pabalochistan.gov.pk/storage/4216/2021-07-16_11_03_05_66551.pdf), sections 2, 71-72; printed page 50. Section 71 specifies twice ordinary pay beyond non-seasonal 9 daily/48 weekly or seasonal 9 daily/50 weekly thresholds, with wage-component exclusions and possible government time rates for piece workers. Section 72 couples OT to conformity with the Act/rules and an adult worker's voluntary choice. Exact commencement/amendment history and Cosmix's factory/worker status require review. The shops piece-rate proviso does not establish a universal piece-worker exclusion across enactments.

## 7. Islamabad Capital Territory

[ICT Labour Department](https://ictadministration.gov.pk/labour-department/) expressly lists implementation of the Payment of Wages Act 1936, Factories Act 1934, Shops Ordinance 1969 and Standing Orders Ordinance 1968 in ICT. This verifies the department's stated enforcement framework, not individual coverage or a current consolidated gazette chain.

### ICT-1: Payment of Wages Act 1936, Act IV of 1936

[Pakistan Code wage Act PDF](https://pakistancode.gov.pk/pdffiles/administrator8820e88efaf7eedabf5c1d8c73b3dee5.pdf), sections 1-2, 7-9, 23; PDF pages 3-9. Header date: 23 April 1936; original commencement is notification-based under section 1. Section 7 limits deduction types; section 9 ordinarily limits absence deduction to absent/required work time. Section 8 requires prior government/prescribed-authority approval, notice/show-cause, age protection, no instalments/collection after 60 days and registered approved beneficial use. The retrieved federal section 8(4) retains an anna expression, unlike Punjab's amended 3% wording. Do not convert that into an active modern cap without authenticated ICT text/interpretation. Contractor coverage, extensions, any wage ceiling and current rules require evidence, not a presumed federal fallback for every province.

### ICT-2: Shops and Establishments Ordinance 1969, Ordinance VIII

[Pakistan Code shops PDF](https://pakistancode.gov.pk/pdffiles/administrator3e8c074aca99ce94319896ecd3769195.pdf), sections 1-2, 4-10, 21; PDF pages 3-9. Header: 3 June 1969. Section 1 conditions immediate application on previously covered areas/classes and retains notification powers. Sections 2/4/5 govern commercial/industrial establishments and exclusions. Sections 7-9 generally address adult rest, 9 daily/48 weekly hours and double ordinary-rate qualified OT; section 9 has a contract piece-rate proviso. Sections 6/10 address weekly holiday pay and spread-over. ICT-specific amendments/exemption notifications and employee coverage were not exhaustively verified.

### ICT-3: alternative factory coverage

[Pakistan Code Factories Act 1934 PDF](https://pakistancode.gov.pk/pdffiles/administratorf38efe0a6e760531f05b4a29ba4ba215.pdf), sections 2, 34-38, 43-47; section 47 on PDF page 33 provides twice ordinary pay beyond non-seasonal 9 daily/48 weekly or seasonal 9 daily/50 weekly thresholds. Section 46 contains a special cross-midnight allocation rule. Determine manufacturing-process, premises, worker and notification coverage before applying this track; the HVAC/MEP business description alone is insufficient. Current ICT consolidation and applicable exemptions remain outstanding.

## 8. Unresolved Cosmix applicability evidence

No employee/location was classified or cleared in this sidecar. The operating provinces and HVAC/MEP installation/maintenance activities are user-confirmed, but that does not establish the following facts:

| Evidence needed | Why it blocks an applicability decision |
|---|---|
| Employer legal entity, establishment registrations and dated branch/site addresses | Company name/Head Office code cannot establish which establishment operated at the duty location. |
| Actual duty locations and transfers, client premises, temporary works and cross-border/midnight intervals | A site assignment may not describe all places at which contractual work occurred. |
| Employment contract, actual functions, reporting/control and hiring/firing powers | `OFFICE`, `SITE`, engineer, supervisor, manager and contractor labels do not determine statutory definitions or exclusions. |
| Contractor/subcontractor/client contracts and responsible employer/principal/occupier | Direct and indirect employment provisions need facts; a client-site registration does not alone classify Cosmix staff. |
| Workshop processes, manufacturing/repair activity, premises, headcount history, power use, factory registration and inspector notices | Office, repair workshop, construction site and factory tracks must be resolved separately. |
| Age/category, apprenticeship, wage basis, compensation components, allowances, piece/time rates and agreements | Definitions, protected categories, ordinary wage and possible piece-rate treatment vary. |
| Required working intervals, genuine breaks, weekly rest/holidays, employer instructions, travel/waiting/standby and approved paid leave | Missing checkout, geofence failure, lunch, paid leave or waiting cannot alone establish unjustified unpaid absence. |
| Gazette commencement/repeal/saving chain, current rules, extension/exemption orders and validity periods | A law title or province selection is insufficient to choose the operative version. |
| Fine authorizations, notices, procedural records, registers and approved use of receipts where applicable | Management's late penalty setting is not statutory fine approval. |
| Formal reviewer/activator appointments and scope; signed determination bound to these facts | The proposed clearance sequence currently has no established officeholders or authority. |

Construction/repair/installation suggests potential contractor or industrial-establishment coverage; engineering offices suggest potential commercial-establishment coverage. These are research inferences, not findings that any particular person is covered. Work at a public hospital, defence installation, mine or client's factory needs separate analysis; do not inherit the client's exclusion or assume ordinary shops rules. Trans-provincial operations do not erase local wage/hour obligations; forum and employer responsibility need a separate determination.

## 9. Recommendations to main implementation

These recommendations are design guards, not changes made or authority granted here.

1. Keep all financial legal activation unavailable. Company/policy publication, OT factual review, previews and saved candidates must not set legal clearance, payroll readiness or payment-release authority. A missing clearance must produce a review/block reason without stopping earned salary.
2. Resolve a dated applicability bundle for the employee, employer, establishment, actual work interval and wage period. Pin jurisdiction evidence, law version/sections, commencement source, classification rationale, exemptions and signed determination. Missing, ambiguous, expired, withdrawn or conflicting evidence fails closed; never infer from city text/Head Office or use a province-wide default.
3. Preserve distinct candidate kinds: ordinary absence shortfall, disciplinary fine, loss/damage, KPI effect and OT. A three-lates/day or fixed penalty exceeding actual shortfall must be flagged for fine classification review. Do not relabel a punitive charge as absence, infer another worker's waiting loss as this employee's absence, or silently convert it to a collectible amount.
4. A future absence validator needs verified unjustified unpaid intervals and the applicable remuneration/denominator determination. The old required-work-period ratio and Punjab Code section 170's wording are not interchangeable. Retain exact intervals and components. Company full/half-day fractions, BASIC-only selection, month-days divisor and rounding must be validated against the signed determination. Reject unsupported/excessive candidates for review rather than silently changing them into approved deductions.
5. Model fine evidence separately: competent statutory authority where required, approved acts/omissions, notice/service, meaningful show-cause and decision records, protected age, wage-period aggregate cap, recovery deadline, instalment prohibition where applicable, register and approved beneficial use. Reconcile other fines in that period, not just attendance fines. Do not assume one cap or deadline across the five jurisdictions or Punjab transition dates.
6. Retain the confirmed company OT formula as an explicitly named company candidate. It is not legal certification. Review office exclusion, common/profile OFF, pre-shift work, weekly excess, rest-day/public-holiday work, break semantics, wage components, consent and exceptional-work permits separately. Statutory entitlement must not disappear because HR rejected intake or no company request exists. Unknown statutory entitlement is unresolved, not zero. Do not automatically add company and statutory candidates or net OT against penalties; the signed determination must specify reconciliation.
7. Calculate daily, weekly, overtime, rest and spread-over observations independently, using the legally determined week/day/midnight rules. Preserve original punch/session evidence and explicit transformations. A nine-hour six-day elapsed schedule might include eight worked hours plus lunch, but the current company no-break OT calculation does not establish that fact. A weekly threshold can arise even when a company daily threshold has not.
8. Apply larger-only overlap suppression and duplicate/cross-month reconciliation as calculation safeguards only. The larger candidate still needs an independent legal basis; suppressing the smaller charge does not validate the larger. Pin salary changes, law/policy revisions, historical wage-period fines and financial inputs so current edits cannot rewrite earlier evidence.
9. If the proposed authority chain is formally confirmed later, use existing authentication, scoped permissions, MFA, concurrency, audit and protected evidence. Do not invent a LegalCounsel/HR classification permission, seed assignments or create a bypass toggle now. A future activation transaction should revalidate formally delegated actors and unchanged evidence, policy/salary/location/law hashes, validity dates and revocation. Preserve prior immutable decisions; any changed material scope requires renewed review.
10. Distinguish legal clearance from operational acceptance. Actual authenticated persistence, competing writers, private evidence/replay, source revocation, transfer/midnight/month-lock behavior and client acceptance remain separate checks. This research proves none of them and does not authorize deployment, SQL or payment execution.

## 10. Exact blockers for activation

| Blocker | Required resolution before dependent activation |
|---|---|
| B01 - Formal authority absent | Confirm and evidence who may appoint counsel, who classifies HR evidence, who accepts a clearance and who activates; bind identities/delegations, scope and validity. The proposed sequence is not an appointment. |
| B02 - No signed applicability determination | An appointed competent reviewer must evaluate the actual employee/establishment/location/contract/wage facts and identify governing provisions and exclusions. A signature on this research is not sufficient. |
| B03 - Punjab commencement/transition unresolved | Obtain section 1(3)'s Gazette notification and operative date; reconcile section 435 savings, duty periods, current rules/exemptions and the October enforcement statement. |
| B04 - Current primary-text completeness unresolved | Confirm amendment/rule/notification/order currency for all five jurisdictions; resolve official date/text discrepancies and visually authenticate the Balochistan wage scan. |
| B05 - HR classification facts missing | Supply the dated evidence in section 8, including actual sites, contractor responsibility, office/workshop/factory classification and worker/category facts. |
| B06 - Financial basis unresolved | Signed determination of remuneration components, absence denominator, paid-time treatment, rounding, deduction kind/caps/procedure and statutory OT/company reconciliation. BASIC/days/9 is not that determination. |
| B07 - Statutory fine evidence missing | Obtain required external authorizations and procedural/register/use evidence, or keep punitive deductions blocked. Internal counsel/HR/SuperAdmin cannot manufacture external statutory approval. |
| B08 - Executable gate and acceptance unverified | Main must demonstrate server-enforced blocking and subsequent scoped/revocable activation only after B01-B07, plus the actual acceptance checks. No such implementation or acceptance occurred here. |

Disposition: all legal financial activation remains BLOCKED. Research and company configuration may be reviewed; no lawful approval, statutory exemption, legal permission or payroll-release decision is asserted.
