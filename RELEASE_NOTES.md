# Sleep Call Keepalive v1.1.0

**A tiny Windows utility for Discord sleep calls that disconnect after long computer inactivity.**

Windows Task Scheduler checks every 15 minutes. After 165 minutes without keyboard or mouse input, if Discord is running and the desktop is unlocked, it sends F15 once and exits. Your microphone, mute state, and Discord installation remain untouched. No AutoHotkey or game list required.

### What's in this release

- The field-tested 1.0.1 F15 core, which logged 20 successful keepalives over several days in its initial real-world test.
- A 64-bit native input-layout self-test to catch the earlier Win32 error 87 bug.
- Safer upgrades: compile and test the replacement executable before swapping it in.
- A branded README and graphics, complete source, MIT license, diagnostics, settings, and clean uninstall.
- A Windows CI build that validates native structure size without sending keyboard input.

### Install

Download **Sleep-Call-Keepalive-Windows-v1.1.0.zip**, extract it, and run `Install.cmd`. Then run `Check Status.cmd` and `Test F15 Input.cmd` once. Leave Windows awake during overnight calls and check `Open Log.cmd` for `SENT` entries the next morning.

### Important limitations

This is an **unsigned** community utility. Smart App Control can block it, and Microsoft does not provide a per-app Smart App Control allowlist. This is not an official Discord product, cannot detect whether a call is active, and cannot prevent network outages or a sleeping PC from ending a call. A successful F15 send does not guarantee that a call will stay connected for every user.
