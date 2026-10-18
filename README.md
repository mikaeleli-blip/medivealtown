# Medieval Town

A medieval village in the same chunky 3/4 pixel-art style as Frontier Ledger. The king lives on the map, and the village is
NPC-run around a **tax chain**: serfs → reeve → bailiff → king.

You're the unseen hand: you lay out the village and set its laws, and the people live their lives.

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

A new king comes to an old village of six serf families with their cottages and strips in the open field, two freemen
and a track through the middle. There is no castle, no church and no mill. The royal family, a knight, a priest and the
king's bailiff arrive with a pavilion and a baggage cart.

A sound opening:

1. **Royal Demesne**: the king's own field. Serfs work it on their week-work days. Without it the court starves once the
   cart's grain runs out.
2. **Royal Granary**: the cart is unloaded into it.
3. **Woodward's Lodge** and **Quarry**: freemen fell timber and break stone. Serfs on week-work help when sites wait.
4. **Windmill**: everyone must grind at the king's mill and leave the crown's share. Bread goes further than pottage.
5. **Bailiff's Hall**, the **Parish Church**, then the **Castle**.
6. More **Cottages** and **Strip Fields** as families grow. A family without a strip goes hungry.

## The tax chain

Each household owes:

- a **harvest tax**: a share of its strip's grain, 15% by default;
- a **hearth tax** in coin each week, plus rent for freemen;
- **tallage**, laid on serf families at Martinmas (start of winter).

| Day | Who | What happens |
|---|---|---|
| Friday | **Reeve** | Walks door to door with a sack and collects what each house owes. Coin they can't pay he takes in grain. He keeps his share (5%) and carries the rest to the Bailiff's Hall. |
| Saturday | **Bailiff** | Carries the tax chest from his hall to the castle and keeps his share (10%). |
| Saturday | **King** | The rest reaches the royal treasury and granary. |

Every officer has a hidden **honesty**:

- **A crooked reeve** squeezes households for more than they owe and pockets it. The village grumbles about him in the
  Chronicle and their loyalty drops. The villagers choose a reeve each spring and prefer honest men, but they can get it
  wrong. You can order them to choose another.
- **A crooked bailiff** keeps more of the chest than his share. It shows in the Court book's tax ledger and in the
  Michaelmas accounts. You can dismiss him, and a new one is sent within a week.

The shares, taxes and week-work are all set in **the Laws**. If loyalty stays near nothing for two weeks, the village
rises and drives the king out, and that reign is over.

## The hierarchy

| Rank | What they do |
|---|---|
| **King** | Holds court in the castle (a pavilion until it's built). Has a temperament (Just, Pious, Greedy, Cruel, Brave, Gentle) that shapes the village. Ages and dies. The eldest son inherits, then a daughter, then kin. A child heir rules through a regent. With no heir, a claimant from another house takes the throne. An unwed king is found a match with a dowry. |
| **Knight** | Rides the village by day. Families are less likely to flee. |
| **Bailiff** | The king's man. Lives in the Bailiff's Hall, keeps the tax chest and carries it to the castle. |
| **Priest** | Says mass on Sundays and takes a tenth of every harvest as tithe. Gives alms from it to the hungry. |
| **Reeve** | A serf chosen by the villagers each spring. Collects the taxes, and while he's in office the demesne yields 15% more. |
| **Freemen** | Work the trades (mill, woods, quarry, alehouse) for the crown's wage. Pay rent, and buy grain and firewood from the granary. |
| **Serfs** | Owe the king week-work: 2 days a week by custom, on his demesne, his building sites, carting stone and timber, gathering his wood. The other days they work their own strips. Sunday is for mass. |

**Loyalty** to the crown drifts toward what folk feel they deserve. It depends on whether they are fed, warm and housed,
the taxes and dues, a reeve who doesn't squeeze them, how devout they are, an evening cup at the alehouse and the
king's temperament. Below 15, families slip away by night. If the village is loyal, new families come.

**The year:** a 70-day year. Spring and summer grow the crop, autumn is the harvest, Michaelmas closes the tax accounts,
Martinmas brings the tallage, Christmas the king's feast for the village (if the granary can bear it), and spring
the new reeve.

![Buildings](docs/buildings.png)

## Controls

| Action | Input |
|---|---|
| Pan | Left-drag on the map, right/middle-drag, WASD or arrows (hold Shift to go faster), or click the minimap |
| Zoom | Mouse wheel, `+` / `-` |
| Recenter | `H` |
| Pause / speeds | `Space`, `1`–`4` (1×, 3×, 10×, 30×). A day lasts 3 minutes at 1×. |
| Tools | `Q` inspect, `R` track, `X` clear trees and rocks, `Esc` or right-click to cancel |
| Build | Hotbar at the bottom: Tracks, Homes, Fields, Works, Church, Crown |
| Inspect | Click a villager or building. Click names in the books to find people on the map. |

The four books at the top right:

- **The Court:** the royal family, the knights, the reeve and bailiff, with buttons to call a new reeve or dismiss
  the bailiff. Also the tax ledger (owed → reeve kept → bailiff kept → reached the castle), the church and the village.
- **The Laws:** harvest tax, hearth tax, tallage, the reeve's and bailiff's shares, week-work, multure, freemen's rent
  and wages, the grain price, the granary reserve, selling surplus, buying materials and the dole.
- **Folk:** everyone, sortable by rank, loyalty, name or age.
- **The Chronicle:** everything that's happened.

## Code

- `Assets/Scripts/Simulation`: engine-free and saved as JSON.
  - `Town.cs`: the map, placing and removing buildings, stores, a new manor.
  - `Town.Day.cs`: the daily plan and the day.
  - `Town.People.cs`: households, housing, strips, trades, births, deaths, marriages, flight, newcomers.
  - `Town.Court.cs`: succession, the reeve and bailiff, the tax chain, Michaelmas accounts, feast days, revolt.
  - `Defs.cs`: buildings, ranks, names and every balance number.
- `Assets/Scripts/Game`: Unity side: `GameController` (clock, input, save), `WorldView` (the map), `Hud`
  (the interface), and `Art/` (all the procedural pixel art, with medieval buildings in `BuildingArt.Medieval.cs`).

![Folk of every rank](docs/people.png)

## Next ideas

- Things for the treasury to spend on: hiring knights, buying land, wars with neighbouring realms.
- More trades: smithy, bakehouse, sheep and wool, a fishpond.
- Manor court days, where the lord fines and judges.
- Interiors when a building is selected, as in Frontier.
- A market charter and a growing town.
