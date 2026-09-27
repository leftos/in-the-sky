
<!-- flight run 2026-09-27T02:59:56.9500220Z -->
| Variant | Tick ms | Flight 1 s | Flight 2 s | Flight 3 s | Ticks/s | Target ticks/s | Headroom | Bytes/tick | Gen0 | Gen1 | Gen2 | Decisions/flight |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| csharp | 250 | 0.21 | 0.22 | 0.20 | 169853 | 256 | 663.49x | 0.0 | 0 | 0 | 0 | 2641 |
| luacs-decision | 250 | 0.23 | 0.22 | 0.24 | 156083 | 256 | 609.70x | 0.0 | 0 | 0 | 0 | 2641 |
| moon-decision | 250 | 0.36 | 0.28 | 0.29 | 116051 | 256 | 453.32x | 2297.4 | 3 | 0 | 0 | 2641 |

<!-- flight run 2026-09-27T03:00:07.4431023Z -->
| Variant | Tick ms | Flight 1 s | Flight 2 s | Flight 3 s | Ticks/s | Target ticks/s | Headroom | Bytes/tick | Gen0 | Gen1 | Gen2 | Decisions/flight |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| luacs-tick | 250 | 35.78 | 34.67 | 39.77 | 980 | 256 | 3.83x | 0.0 | 0 | 0 | 0 | 2641 |
| moon-tick | 250 | 126.10 | 139.73 | 131.53 | 272 | 256 | 1.06x | 7694468.0 | 16557 | 109 | 3 | 2641 |

<!-- flight run 2026-09-27T03:11:25.9963466Z -->
| Variant | Tick ms | Flight 1 s | Flight 2 s | Flight 3 s | Ticks/s | Target ticks/s | Headroom | Bytes/tick | Gen0 | Gen1 | Gen2 | Decisions/flight |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| luacs-decision-budget | 250 | 0.28 | 0.27 | 0.26 | 134275 | 256 | 524.51x | 0.0 | 0 | 0 | 0 | 2641 |
| moon-decision-budget | 250 | 0.49 | 0.71 | 0.59 | 60203 | 256 | 235.17x | 156233.8 | 805 | 802 | 802 | 2641 |
| luacs-tick-budget | 250 | 40.54 | 39.66 | 38.59 | 909 | 256 | 3.55x | 0.0 | 0 | 0 | 0 | 2641 |

<!-- flight run 2026-09-27T03:29:46.3818345Z -->
| Variant | Tick ms | Flight 1 s | Flight 2 s | Flight 3 s | Ticks/s | Target ticks/s | Headroom | Bytes/tick | Gen0 | Gen1 | Gen2 | Decisions/flight |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| moon-tick-budget (first 200 ticks) | 250 | 4.41 | 5.32 | 6.26 | 38 | 256 | 0.15x | 428766812.9 | 8158 | 8071 | 8071 | 200 |
| moon-tick-budget (first 200 ticks) | 500 | 4.23 | 5.24 | 4.46 | 43 | 128 | 0.34x | 428765554.9 | 7886 | 7799 | 7799 | 200 |
| moon-tick-budget (first 200 ticks) | 1000 | 4.44 | 5.22 | 5.34 | 40 | 64 | 0.62x | 428884778.5 | 7605 | 7518 | 7518 | 211 |
