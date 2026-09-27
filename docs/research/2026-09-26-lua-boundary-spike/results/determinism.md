
<!-- determinism run 2026-09-27T03:30:58.1344727Z; C# baseline hash AE6C1F31685AB13C90D4F2DC1EC15BC83ECACA9F060F20ADC52B512376B86C24; 2705 decisions; end activities: sit 38, sleep 52, drink 48, eat 1, lav 10, read 1, ife 37, walk 0, talk 10, call 3 -->
| Variant | Run 1 | Run 2 | Fresh process | Three match | Equals C# baseline |
|---|---|---|---|---|---|
| csharp | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| luacs-decision | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| luacs-tick | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| moon-decision | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| moon-tick | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| luacs-decision-budget | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| luacs-tick-budget | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| moon-decision-budget | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |

| Runtime | pairs order (run 1) | Run 2 same | Fresh process same |
|---|---|---|---|
| luacs | `1,2,7,k17,k42,k8,k1,k29,k11,k5,zeta,alpha,mid,k100,k64,k2,1000,3.5,k99` | yes | yes |
| moon | `k17,k42,k8,k1,k29,k11,k5,zeta,alpha,mid,k100,k64,k2,1000,7,3.5,k99,1,2` | yes | yes |

<!-- determinism run 2026-09-27T03:43:12.3213136Z; C# baseline hash 24F3C106A7E53A952AB6A86EBF721E31F40D2F8B86212BEE8A983B93EBD47483; 200 decisions; end activities: sit 105, sleep 9, drink 72, eat 0, lav 0, read 0, ife 10, walk 0, talk 4, call 0 -->
| Variant | Run 1 | Run 2 | Fresh process | Three match | Equals C# baseline |
|---|---|---|---|---|---|
| moon-tick-budget (first 200 ticks) | 24F3C106A7E53A95 | 24F3C106A7E53A95 | 24F3C106A7E53A95 | yes | yes |

| Runtime | pairs order (run 1) | Run 2 same | Fresh process same |
|---|---|---|---|
| luacs | `1,2,7,k17,k42,k8,k1,k29,k11,k5,zeta,alpha,mid,k100,k64,k2,1000,3.5,k99` | yes | yes |
| moon | `k17,k42,k8,k1,k29,k11,k5,zeta,alpha,mid,k100,k64,k2,1000,7,3.5,k99,1,2` | yes | yes |
