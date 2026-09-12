# Release notes

Each ClipPull version has a Markdown file named after its Git tag, for example
`v0.6.0.md`. The build workflow requires the matching file and uses it as the
GitHub Release body.

Use `{{SIGNING_STATUS}}` where the workflow should insert the actual signing
status of the produced executable.
