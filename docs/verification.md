# Verification record

Verified locally on 26 September 2026 with the .NET 8 SDK.

## Automated checks

The minimal-theme update on 29 September 2026 passed all 72 API/rendering tests
with no skipped tests. The web/API build succeeded and stylesheet content-hash
checks passed. The workspace stylesheet now defines a light neutral sidebar,
white content surfaces, smaller typography and icons, flat bordered cards, compact
metrics, and restrained status accents. Decorative card stripes, hover lifts,
large title icon backgrounds, and repeated workspace/footer text were removed.
Navigation remains icons and labels; filtering, editing, exports, and discovery
behavior are unchanged. Existing mobile menu, short-window scrolling, keyboard
focus styles, and reduced-motion handling remain in place.

The browser tool still failed during setup before a visual inspection was possible.
Remaining visual checks: desktop and narrow layouts, long component names, focus
and active states, all detail tabs, and graph controls. No browser screenshot or
visual sign-off is claimed.

The component-tile follow-up on 29 September 2026 passed all 72 API/rendering
tests. Tiles now show only nonzero interaction counts with separate, spaced labels
and singular/plural wording. An empty component has one concise placeholder.
Stylesheet URLs now carry content hashes using the framework file-version service;
the HTTP regression test verifies that both versioned URLs serve the current CSS
and their versions equal the actual file hashes. Visual browser verification
remains outstanding.

The content-layout update was verified on 29 September 2026. The solution built
with zero warnings/errors and all 71 API/rendering tests passed (none skipped).
The new rendering tests cover populated, empty and failed component-message views,
HTML encoding, preserved routing details, and the integration map/documentation
shown on the default tab. Existing component, saga, catalogue, system and navigation
rendering checks passed alongside the API workflows. The initial run exposed test
harness setup errors (empty ParameterView and missing popover provider); those were
corrected before the successful full API-suite rerun.

The main pages now use shorter headings, explicit view switches, compact capability
summaries, and expandable technical details. Component interactions separate
incoming and outgoing work. Imported integration details have six tabs: Overview,
Connections, Components, Messages, Operations, and Source & history. Discovered
integrations use Overview for the graph and Review for matching findings. Definition
formats, persistence, and discovery behavior are unchanged.

Browser setup was attempted for this update but failed before connecting with
`missing field sandboxPolicy`. No visual browser pass is claimed. Remaining smoke
checks: navigate all six main pages; open the source selectors on Messages; inspect
each integration tab and disclosure; check the graph after switching tabs; compare
mobile and desktop layouts with long names; confirm that navigation still has only
icons and labels.


The NServiceBus processing update was verified on 27 September 2026: **305 .NET
tests passed** (API/rendering 67, Application 74, Domain 15, Infrastructure 149),
with none skipped. The full solution run passed 304 tests; the Application suite
then passed all 74 tests after the final deterministic-discovery regression was
added. The solution build completed with zero warnings and zero errors.

New checks cover handler/saga references, wrong-direction bindings, duplicate IDs,
send-only restrictions, Property/Header/Custom/SagaId correlation, multiple starters
and processors, not-found policies, timeout schedules/cycles/reachability,
completion warnings, rule limits, and JSON/YAML sample round trips. API tests
verify processing evidence, search, Markdown, exports, rejected writes, edits,
immutable history, archive/restore, and three routes rather than duplicate broker
connections. SQLite reload preserves saga state-type declarations and timeout
rules. HTML rendering checks cover local rules, conditions, and text encoding.

No database migration is needed. Tests use isolated databases; no user catalogue
was altered. This is declarative processing metadata, not runtime NServiceBus
execution or instance telemetry. No live NServiceBus/SQL Server integration or
visual browser verification is claimed. For a UI smoke test, import the three
[saga examples](nservicebus-processing.md#try-the-complete-example), inspect
**Handlers & sagas** in the component and generated integration, expand connection
processing evidence, edit a rule, and check validation preview and revision history.


The unified-message update was verified on 27 September 2026: all 270 .NET tests
passed across API/rendering (65), Application (73), Domain (15) and Infrastructure
(117). The first three suites passed in the solution run; the infrastructure suite
passed on rerun after correcting a raw-string syntax error in a new test fixture.
No tests were skipped. Coverage includes exact lowercase actions, action/type rules,
required subscriptions, invalid mixed formats, duplicate IDs/routes, empty lists,
size limits, YAML/JSON exports, old/new component matching, summary/catalogue counts,
rendered action labels and HTML encoding, rejected updates preserving history, and
legacy stored records. Existing API workflows exercise unified examples through
discovery, graph/impact views, editing, archive/restore and persistence.

Unified messages are supported by the current component schema with recommended
`schemaVersion: "1.1"`; independent HTTP contract matching is still a proposal.
Original sources are not rewritten and no migration is required. Live browser
visual verification remains unavailable; the UI checks use the HTML renderer.

The technology-list update was verified on 27 September 2026: all 237 .NET tests
passed (API/rendering 63, Application 70, Domain 15, Infrastructure 89), with no
skipped tests. New cases cover list and legacy scalar parsing, empty/default values,
invalid entries and limits, JSON/YAML round trips, old stored canonical JSON,
archive/restore with unchanged historical source/hash, API persistence, technology
search, discovered snapshot validation and revision retention. Component details
now render separate technology tags. Live visual verification remains unavailable.
No database migration is required. The component detail API and formatted exports
now return a technology array; existing definition strings remain readable.

The workflow-card full-solution run passed all 210 .NET tests and 7 JavaScript tests, with no skipped tests. The 70 application tests were rerun successfully after the final word-wrapping and Unicode refinement. Both graph JavaScript modules pass Node syntax checks.

| Suite | Passed | Failed |
| --- | ---: | ---: |
| IntegrationHub.Api.Tests | 52 | 0 |
| IntegrationHub.Application.Tests | 70 | 0 |
| IntegrationHub.Domain.Tests | 15 | 0 |
| IntegrationHub.Infrastructure.Tests | 73 | 0 |
| Graph viewport JavaScript tests | 7 | 0 |

The solution compiles with nullable references and warnings treated as errors. An earlier Release publish exists at `artifacts/publish`; rebuild it before running the latest changes from that folder. The SQL Server idempotent migration script is generated at `artifacts/migrations.sql`; generated artifacts are intentionally git-ignored.

Coverage includes nonlinear graph construction, cycles and long traversal, shared identities, message consumers, escaping, JSON/YAML parsing and schema equivalence, round-trip exports, SQL-based search, relational mapping, transactional rollback, immutable revisions, conflicts, archival, complete API workflows, CSRF, role authorization, production authentication safeguards and readiness checks. Static CSS, JavaScript and the local Mermaid ESM entry point are verified through HTTP with their executable MIME types.

## Systems catalogue update

The Systems page now reads the component catalogue as well as the existing system
registry. System components (`InternalSystem`, `ExternalSystem`, `SaaS`) link to
their component details; registered systems retain their original detail routes.
Archived components are omitted, while retired entries display their status.
The empty state explains the system/component distinction and the caller template.
API errors no longer display a misleading empty-systems message.

All 62 API/rendering tests passed. Six new rendering cases cover empty, registry-only,
component-only and mixed catalogues, all component types, archived/retired entries,
identical IDs with distinct routes, API failure and registered detail rendering with
HTML encoding. This change does not modify API contracts, persistence or discovery.
Live browser verification remains unavailable due to the connection failure noted below.

## Modern icons and navigation update

The workspace now uses original inline SVG symbols with consistent stroke weights,
including a distinct icon for every one of the 20 component types. Navigation is
grouped into Workspace, Explore and Authoring, with a dark teal sidebar and a
collapsible mobile menu. Component cards, status badges, interaction headings and
graph controls share the same visual language. Discovered integration details keep
Integrations selected; the separate discovery navigation item remains hidden.

All 56 API/rendering tests and all 7 JavaScript viewport tests passed. New rendering
checks exercise every component icon through MudBlazor and verify navigation groups,
the selected integration route and mobile menu accessibility attributes. The graph
viewer passes its JavaScript syntax check, and fullscreen updates preserve its icon.
`git diff --check` passes. No catalogue data or discovery rules were changed.

The in-app browser bootstrap still fails before execution (`missing field sandboxPolicy`).
Actual visual layout and interaction checks remain outstanding: inspect desktop and
mobile widths, open/close the mobile menu, check short-height sidebar scrolling,
and exercise graph fullscreen. These automated checks do not constitute a live
browser visual pass.

## Workspace readability update

The shared workspace stylesheet increases body and metadata text sizes, strengthens
contrast, and standardizes spacing, surfaces and responsive layouts. Components
default to cards with an optional table view. Their details separate interactions,
connected integrations/findings, and source history into tabs. HTTP routes and
message declarations show their purpose first, with routing/payload details in
expandable sections. Findings display a suggested next step above their technical
reference. The editor includes write/review/save steps and YAML formatting help.

The dashboard totals and recent rows include discovered integrations; authored-only
breakdowns are labelled separately. The separate discovery navigation item remains
hidden. All 54 API/rendering tests pass, including a populated-dashboard regression
test, existing escaping/link checks, and HTTP delivery of the new stylesheet.
`git diff --check` passes. No database or discovery matching behavior was changed.

The in-app browser connection failed before execution (`missing field sandboxPolicy`).
Live visual validation is outstanding: check the dashboard, catalogue cards/table,
component tabs, editor and expanded technical details at desktop and mobile widths;
also verify sidebar scrolling at short heights and keyboard navigation.

## Combined integrations catalogue verification

The Integrations and Search pages now use `/api/catalogue`, which combines authored
definitions and current discovered networks. All 51 API/rendering tests and all
12 repository tests passed after this change. Tests cover empty and unlinked-only
catalogues, mixed kinds, identical IDs with distinct detail routes, combined totals
and pagination, out-of-range pages, kind/search/metadata/direction filters, component
edits, archive/restore, and card/table rendering against the combined API response.
The existing authored search and persistence checks also pass. No data migration or
conversion of generated networks into authored records occurs.

Browser setup failed before execution (`missing field sandboxPolicy`), so live
visual verification remains outstanding. Restart the app, open **Integrations**,
confirm the **All integrations** selection, switch between cards and table, and
open a **Discovered** entry. The kind dropdown refreshes the results automatically.
The baseline Release publish listed above predates this change; `dotnet run`
rebuilds the updated application.

## Error banner regression

Rendered Dashboard tests reproduced the false literal `Error` banner before the fix. They now verify that a successful state has no alert and that a failure displays its actual message. All seven page bindings now pass the error variable correctly. The same string-binding issue was corrected for architecture and dependency diagram inputs. This verification uses the ASP.NET component HTML renderer; it does not claim a browser automation pass.

## Graph viewer

The shared viewer now includes zoom, pan, fit, original-size view, horizontal/vertical layout, fullscreen and whole-graph SVG download. Server-rendered component checks cover accessible controls and the legend; API tests verify all three graph JavaScript modules are served with executable MIME types. Generator tests cover all 20 component types and retention of optional-node styling. Seven Node tests cover wide/tall graph fitting, hidden viewports, original-size centring, pointer-anchored zoom, zoom limits and layout changes preserving labels and relationships.

Workflow cards add original inline vector icons for all 20 node types, separate type captions, wrapped names, rounded white surfaces, shadows, hover/focus emphasis and directional port indicators. Optional borders and asynchronous links remain distinct. Decoration uses the measured Mermaid rectangles without moving their edge anchors. SVG definitions and styles are included inside the exported graph. An offscreen measurement surface supports graphs inside hidden tabs. Generator checks cover safe long/Unicode labels and bidirectional port metadata.

Browser connection setup was attempted again but failed before execution (`missing field sandboxPolicy`). Card appearance, ports, hover/focus, hidden-tab measurement, exported SVG appearance, pointer gestures, fullscreen, download and the visual Mermaid layout still need a browser smoke test: open an integration's Architecture tab, try both layouts, drag and zoom, use Fit/100%, enter/exit fullscreen, download SVG, then repeat in the definition preview and global dependency view. Navigate away and back to check viewer cleanup.

## SQLite development verification

The default Development profile now creates and migrates a real file database at `src/IntegrationHub.Api/App_Data/integrationhub.db`. Startup creates no catalogue entries. API tests use the same SQLite registration and startup migration path with isolated temporary files. The full import/update/archive workflow, SQL search and version history pass. Additional tests verify provider guards, separate SQL Server/SQLite migration sets, relative file paths, foreign-key enforcement and preservation of definitions and revisions across a new service provider and repeated migrations.

Browser automation was retried for this change and still failed before connecting. No visual UI pass is claimed.

## Component-driven discovery verification

Independent API/function/receiver definitions were imported into isolated migrated SQLite databases in consumer-first order. Tests verify generated connections, exact identity matching, payload assertions, ambiguous HTTP targets, fan-out, competing subscriptions/groups, multiple publishers, cycles, self-delivery, disconnected networks, membership changes, retired/archived components, deterministic output and protective limits. The complete HTTP workflow covers preview without writes, discovery, graphs, documentation, snapshot export through the existing integration parser, component message catalogue, dashboard counts and global impact.

Update/archive/restore tests verify disappearing and reappearing connections, stale network URLs, duplicate and stale writes, immutable revision history, role restrictions and CSRF. A simultaneous-edit test verifies only one revision-1 writer succeeds and no history is lost. An upgrade test creates an existing authored integration using the original SQLite migration, applies the additive component migration, and verifies preservation of that integration and component history across service-provider restarts. Both EF provider snapshots match their models; SQL Server execution remains unverified.

The new route endpoints serve the interactive application shell. Separate ASP.NET HTML renderer tests verify component authoring/discovery page controls, binding/finding values and encoding of authored text. The application intentionally disables prerendering, so HTTP page responses alone do not verify interactive page content. Browser connection setup again failed before execution (`missing field sandboxPolicy`); a live visual interaction pass remains outstanding. Follow the complete UI smoke test in [component-discovery.md](component-discovery.md#try-the-complete-example).

## Checks not completed in this environment

The HTTP/messaging refinement is verified through a system → API → event → function → HTTP receiver workflow and an API → command → worker workflow. Tests cover endpoint identity, method/version/environment mismatch, missing or retired targets, duplicate declarations, HTTP cycles, command typing, legacy definitions, broker-only message catalogues and component UI labels. No database migration is needed for these canonical JSON additions.

The fixed desktop sidebar now scrolls within the viewport, does not shrink its children, and wraps long menu labels. Short-window spacing is reduced, while mobile navigation remains in normal document flow. The browser connection still fails before setup, so actual viewport layout/scrolling needs a visual smoke test at a short desktop height, increased browser zoom and mobile width.

- SQL Server execution and Docker Compose startup: Docker/SQL Server were not available. Relational repository and API tests use isolated SQLite databases. The SQL Server migration was generated and inspected, not applied to a live server.
- Visual browser testing: the in-app browser tool failed during connection setup before any page could be inspected. API and static-asset tests passed, but rendered Blazor interaction and actual Mermaid layout still require a browser smoke test.
- Microsoft Entra ID: no tenant registration or credentials were supplied. Development roles and the production-mode guard are tested; real OIDC/JWT sign-in requires deployment configuration.

## Deployment smoke test

1. For local SQLite, run `dotnet run --project src/IntegrationHub.Api`. For SQL Server deployment, configure `.env` and run Compose on a supported host, or follow the SQL Server native instructions in the README.
2. Check `/health/live`, `/health/ready` and `/swagger`.
3. Confirm the empty dashboard and catalogue contain no invented metrics or seeded records.
4. Import an organisation-owned definition; validate its diagram, documentation, message catalogue, search and impact results.
5. Edit it, verify history and test a stale edit from another session.
6. Configure Entra in a non-Development environment; verify Viewer/Editor/Admin access, cookie CSRF, bearer tokens and HTTPS.
7. Verify database backups, restricted credentials, Data Protection key persistence, monitoring and restore procedures before production rollout.
