# FsAssay Desktop & Documentation Handoff

## Summary of Work
1. **Desktop App Migration**: We successfully pivoted from the web-based `FsAssay.Web` to a new `FsAssay.Desktop` application.
2. **Avalonia UI with AOT**: The desktop app is built using Avalonia UI and F#, configured for Native AOT deployment. A sleek dark-mode UI was implemented to run the FsAssay "stunts" (live TAST scanning and Auto-Fix capabilities).
3. **Stunning Documentation Website**: Created a beautiful, Material-inspired documentation website using HTML and CSS in the `docs-website` folder. It features a glassmorphism nav, gradient text, and hover animations.

## Lessons Learned the Hard Way
1. **MSBuild Target Execution**: When integrating `FsAssay.targets` into the solution, the quality gate hook executed on our *own* UI project, causing infinite build failures due to FsAssay dogfooding its own violations. We learned to exclude `FsAssay.Desktop` from the `Condition` in MSBuild targets to allow the UI to compile unhindered.
2. **Central Package Management**: Attempting to add Avalonia with specific versions failed initially because the `Directory.Packages.props` enforces central versions. The correct approach was adding the versions to the `.props` file and omitting them in `FsAssay.Desktop.fsproj`.

## Questions for You
- Would you like me to wire up the actual `FsAssay.Orchestrator` to the Avalonia "Run Scan" button, or keep it mocked for now?
- How do you want to publish the Avalonia app? (e.g., single-file executable for Windows)?
- For the website, should we deploy it to GitHub Pages or another hosting provider?
