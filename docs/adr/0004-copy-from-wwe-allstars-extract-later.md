# Copy shared code from WWE All Stars; extract a library later

The skeleton, ReXGlue patches, Native renderer, launcher, crash reporting and settings code are copied from `WWE_ALLSTARS-RECOMP` into this repo and allowed to diverge. The duplication is deliberate. Extracting a shared library now would freeze its boundaries before the DLL path and Top Spin 4's renderer needs (XPS, resolve readback) are understood. A shared library is reconsidered once a third port shows which parts really are common.
