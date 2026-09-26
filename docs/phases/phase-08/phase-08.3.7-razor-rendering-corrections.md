# Phase 8.3.7 — Razor rendering corrections

Release note extracted from the original project README.


Corrected dashboard Razor expressions that were being emitted as literal text rather than evaluated values. Arithmetic and negative currency expressions are now enclosed as complete Razor expressions before formatting. The remembered-payment picker was also restructured so its conditional searchable input/select markup is parsed as Razor rather than displayed on screen.
