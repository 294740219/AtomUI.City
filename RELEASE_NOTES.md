# AtomUI.City Release Notes

## 1.0.0

### New features

- First stable AtomUI.City package line for Avalonia and AtomUI desktop applications.
- Product-level contracts for Core host lifecycle, module ordering, diagnostics, DI markers, and dispatcher abstraction.
- Runtime modules for Routing, Presentation, MVVM, State, EventBus, Localization, Security, Data, and PluginSystem.
- Build, source generator, template, CLI, and testing packages with local release gates.
- Application and plugin templates that generate buildable/testable projects with Testing layer metadata.

### Breaking changes

- This is the first stable package line. APIs, diagnostics, generated output, template variables, package layout, and CLI JSON contracts documented for 1.0 are now treated as compatibility commitments.

### Fixes

- Hardened project inventory and dependency boundary gates so template payload projects are not treated as repository source projects.
- Hardened generated test templates with `AtomUI.City.Testing` references and `TestLayerNames.TemplateSmoke` metadata.
- Verified package generation, package validation, public API, documentation, CI-equivalent, platform integration, and template smoke gates for the 1.0 release line.

### Known limitations

- Platform integration coverage is intentionally narrow in 1.0 and currently validates the Avalonia dispatcher bridge contract.
- Templates target the current AtomUI.City package family and are not a replacement for application-specific architecture decisions.
- Plugin package compatibility is enforced through manifest and package-layout contracts; host-side dynamic unload scenarios should still be validated by applications that use dynamic plugins.

### Migration notes

- Projects created from earlier local 0.x packages should update `AtomUI.City.*` package references to `1.0.0`.
- Regenerate or review template-created test projects so they reference `AtomUI.City.Testing` and include `TestLayer` metadata.
- Re-run local release gates after upgrading package references: build, tests, docs, public API, package validation, and template smoke.

### Plugin API compatibility

- Plugin API compatibility starts at `1.0`.
- Plugin manifests, package layout, capability declarations, dependency validation, and unload state contracts documented for 1.0 are compatibility commitments.
- Plugin packages should declare host compatibility against the 1.0 package line unless they require a newer documented contract.
