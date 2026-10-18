# Medieval Town

A medieval manor in the same chunky 3/4 pixel-art style as Frontier Ledger. The village is NPC-run.
The game is about **hierarchy**: the king above, the lord and his heirs, the priest and the church,
the reeve the villagers choose, freemen, and the serfs who owe the lord their labour.

You are the lord's steward. You lay out the manor and set its rules, and the villagers live their lives.

![The village in spring](docs/village-spring.png)

Built in Unity 6 (6000.6.5f1). All art is generated in code, so there are no image assets. The art engine
(pixel canvas, terrain, trees, people, camera, UI kit and pixel font) is forked from Frontier Ledger.
The simulation is new.

## Open it

1. Unity Hub → **Add** → **Add project from disk** → pick this folder.
2. Open it with Unity 6000.6.5f1.
3. The first time it opens, the project creates `Assets/Scenes/Main.unity` and opens it.
4. Press **Play**. The game builds itself from code, so the scene stays empty in the editor.

The manor saves once a week and when you quit, to `Application.persistentDataPath/medieval-town-save-v1.json`.
**Start a new manor** is at the bottom of the Manor Rules book.

## The start

The king grants the manor to a new lord. It is an old village of six serf families with their cottages and strips
in the open field, two freemen and a track through the middle. There is no keep, no church and no mill. The lord's
household (lord, lady, children, a household knight and a priest) arrives with a pavilion and a baggage cart.

A sound opening:

1. **Demesne Field**: the lord's own field. Serfs work it on their week-work days. Without it the household starves
   once the cart's grain runs out.
2. **Granary**: the cart is unloaded into it.
3. **Woodward's Lodge** and **Quarry**: freemen fell timber and break stone. Serfs on week-work help when sites wait.
4. **Windmill**: everyone must grind at the lord's mill and leave his share. Bread goes further than pottage.
5. **Parish Church**, then the **Keep**.
6. More **Cottages** and **Strip Fields** as families grow. A family without a strip goes hungry.

## The hierarchy

| Rank | What they do |
|---|---|
| **King** | Far away. Takes tribute every Michaelmas (end of autumn): 20c plus 2c per grown person. Royal favour rises with full tribute and a loyal, devout village, and falls with arrears, unrest and families fleeing. At 0 he takes the manor back. |
| **Lord** | Holds court in the keep, or the pavilion until it's built. Has a temperament (Just, Pious, Greedy, Cruel, Brave, Gentle) that shapes the manor. Ages and dies. The eldest son inherits, then a daughter, then kin. A child heir rules through a regent, usually his mother. With no heir, the manor falls back to the crown and the king grants it to a new house. An unmarried lord gets a bride with a dowry from the king. |
| **Knight** | The household knight rides the village by day. Families are less likely to flee. |
| **Priest** | Says mass on Sundays and takes a tenth of every harvest as tithe. Gives alms from it to the hungry. The bishop sends a new priest when one dies. |
| **Reeve** | Chosen by the villagers each spring from among the serfs. While he's in office the demesne yields 15% more. |
| **Freemen** | Work the trades (mill, woods, quarry, alehouse) for the lord's wage. Pay rent in coin, and buy grain and firewood from the granary. |
| **Serfs** | Owe the lord week-work: 2 days a week by custom. They build his buildings, work his demesne, cart stone and timber, and gather his wood. The other days they work their own strips. Sunday is for mass. They pay tallage at Martinmas, merchet when they marry and heriot when they die. |

**Loyalty** to the lord drifts toward what folk feel they deserve. It depends on whether they are fed, warm and
housed, how burdened they are, how devout they are, an evening cup at the alehouse, the lord's temperament and whether
there's a keep. Below 15, families slip away by night. If the manor is loyal, new families come seeking the lord's
protection.

**The year:** a 70-day year. Spring and summer grow the crop, autumn is the harvest, Michaelmas brings the tribute,
Martinmas the tallage, Christmas the lord's feast for the village (if the granary can bear it), and spring the new
reeve.

![Buildings](docs/buildings.png)

## Controls

| Action | Input |
|---|---|
| Pan | Left-drag on the map, right/middle-drag, WASD or arrows (hold Shift to go faster), or click the minimap |
| Zoom | Mouse wheel, `+` / `-` |
| Recenter | `H` |
| Pause / speeds | `Space`, `1`–`4` (1×, 3×, 10×, 30×). A day lasts 3 minutes at 1×. |
| Tools | `Q` inspect, `R` track, `X` clear trees and rocks, `Esc` or right-click to cancel |
| Build | Hotbar at the bottom: Tracks, Homes, Fields, Works, Church |
| Inspect | Click a villager or building. Click names in the books to find people on the map. |

The four books at the top right:

- **The Court:** the king and his favour, the tribute due, the lord's house and heir, the household knight, the
  church and the reeve.
- **Manor Rules:** week-work days, tallage, multure, freemen's rent and wages, the grain price, the granary reserve,
  selling surplus, buying materials and the dole.
- **Folk:** everyone, sortable by rank, loyalty, name or age.
- **The Chronicle:** everything that's happened.

## Code

- `Assets/Scripts/Simulation`: engine-free and saved as JSON.
  - `Town.cs`: the map, placing and removing buildings, stores, a new manor.
  - `Town.Day.cs`: the daily plan and the day.
  - `Town.People.cs`: households, housing, strips, trades, births, deaths, marriages, flight, newcomers.
  - `Town.Court.cs`: succession, the reeve, the crown, tribute, feast days.
  - `Defs.cs`: buildings, ranks, names and every balance number.
- `Assets/Scripts/Game`: Unity side: `GameController` (clock, input, save), `WorldView` (the map), `Hud`
  (the interface), and `Art/` (all the procedural pixel art, with medieval buildings in `BuildingArt.Medieval.cs`).

![Folk of every rank](docs/people.png)

## Next ideas

- The king's wars: summons for knights, or scutage in coin.
- More trades: smithy, bakehouse, sheep and wool, a fishpond.
- Manor court days, where the lord fines and judges.
- Interiors when a building is selected, as in Frontier.
- A market charter and a growing town.
