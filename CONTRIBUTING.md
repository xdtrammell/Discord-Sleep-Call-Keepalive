# Contributing

Thanks for helping make Sleep Call Keepalive more reliable.

Please keep changes small and transparent. Explain the behavior change, how you tested it on Windows 10 or 11, and whether it affects Task Scheduler, the idle timer, or input injection. Do not add Discord client hooks, token handling, anti-cheat evasion, telemetry, network requests, or invasive permissions.

The installer compiles `DiscordKeepAlive.cs` locally. For changes to the native interop layout, run `DiscordKeepAlive.exe --self-test`, check the status file for `40 bytes (expected 40)` on 64-bit Windows, then perform a manual `--test-input` when no game is open. CI performs the layout test only. It cannot test real overnight Discord behavior.

For bug reports, include the Windows build, app version, relevant log lines, the task state, whether Windows slept, and the result of **Check Status.cmd**. Redact identifying details before posting.
