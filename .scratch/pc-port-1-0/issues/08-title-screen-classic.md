# 08: Title screen on Classic, no server wait

**What to build:** The game reaches its title screen on the Classic renderer without waiting on the dead title server or the DLC check. This is milestone M2.

**Blocked by:** 07 (Hand-off to Swing), 04 (TU1 symbol map), 01 (Oracle Baseline)

**Status:** ready-for-agent

- [ ] Title-server login and patch request are short-circuited to the offline result through hooks
- [ ] The DLC check finishes immediately
- [ ] Boot and loading screens drawn by Loader display
- [ ] The title screen is reached reliably on several consecutive smoke runs
- [ ] A title-screen screenshot matches the Oracle reference capture, apart from differences recorded in the Baseline
- [ ] Boot-to-title time is no worse than the Baseline
