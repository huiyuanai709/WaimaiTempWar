# Delivery Temperature War

外卖温度战争：你是电动车外卖骑手。保温箱里的热汤面随时在变凉，风、开盖、洒漏和碰撞都会偷走温度。在一条短街区里把这单送到，还要尽量别洒、别压扁。

You are an e-bike courier. The order starts hot and cools with time, speed, wind, an open lid, spills, and crushed insulation. Ride one short block, survive a couple of street events, and hand the food over while it is still hot.

This is a small Godot **4.7+ .NET / C#** prototype. Art is built from primitives. Physics is **Jolt**. The cooler is two rigid panels on soft 6DOF springs (the belly sags and bends; a hit crushes it), with a hinged lid and a meal box that can tip. Godot 4.7's `SoftBody3D` keeps vertices in world space here and pinned points do not follow the moving rack, so that path is optional (`WAIMAI_SOFT=1`) rather than the one you play.

## Open and run

1. Install [Godot 4.7 .NET](https://godotengine.org/download) or a newer Godot 4.x **.NET** build (the standard build cannot compile C#), plus the **.NET 8 SDK**.
2. Open this folder in Godot (`project.godot`). Let the editor build the C# solution if it asks.
3. Press **F5** (or the Play button). The main scene is `scenes/Main.tscn`.
4. The street, bike, and cooler are created when the scene runs.

Project settings already select **Jolt Physics**. Keep that engine; the spring cooler and the optional soft bag both expect it.

Headless check from the project root (optional):

```bash
godot --headless --path . -- --autotest
```

That rides the block on autopilot, prints `AUTOTEST_JSON`, and exits 0 when the cooler sags, an event fires, the drop-off resolves, and an achievement is saved.

Force a specific event set with:

```bash
WAIMAI_EVENTS=red,rain,gate godot --path .
```

Names: `red`, `brake`, `dog`, `rain`, `elevator`, `gate`.

## Controls

| Key | Action |
| --- | --- |
| W / Up | Throttle |
| S / Down | Brake, then reverse |
| A / D or Left / Right | Steer |
| Space | Handbrake |
| F | Slam the lid shut |
| Tab | Achievement list |
| R | Restart the run |
| F9 | Clear saved achievements |

## How a run works

The order is hot noodle soup. It starts at **86°C**. You win at the green drop-off pad if it is still **at least 60°C** and less than **50%** spilled. Crush does not fail the order by itself; it ruins the insulation and cools the food faster.

Cooling comes from time, riding speed (wind), rain gusts, an open lid, spill, and crush. The HUD shows temperature, spill, crush, ETA, and speed. The lid glows hotter while the soup is hot. Press **F** if a gust or a hit pops it open.

Each run arms two street events plus one arrival delay. The ticket on the HUD lists them. Driving through a marked stretch triggers the event:

- **红灯 Red light** — stop behind the line. Riding through spawns cross traffic.
- **前车急刹 Sudden brake** — a car stops in your lane. Brake or go around. Surviving with almost no new crush unlocks an achievement.
- **狗追车 Dog chase** — a dog paces you and shoves the bike. The bag and the container wobble.
- **风雨 Rain gust** — side wind, faster cooling, and a shove on the lid and the cooler.
- **电梯 Elevator** or **门禁 Wrong gate code** — at the building you wait, and the soup keeps cooling.

## Achievements

Saved with `ConfigFile` at `user://achievements.cfg` (on Linux that is typically `~/.local/share/godot/app_userdata/Delivery Temperature War/achievements.cfg`).

| Id | Name | How |
| --- | --- | --- |
| `hot_delivery` | 首单热达 | Deliver at or above 60°C without a spill-out |
| `zero_spill` | 滴汤不洒 | Finish with under 1% spilled |
| `hard_brake` | 急刹不碎 | Get the sudden-brake event and keep new crush under 8% |
| `triple_event` | 一路三事 | Trigger three different events in one run |

Tab opens the list. Unlocking shows a toast. F9 wipes the file.

## Layout

- `scenes/Main.tscn` — chase camera, lights, and the nodes the scripts drive
- `scripts/` — C# gameplay only (bike, spring cooler, temperature, events, achievements, HUD, street)
- `assets/fonts/` — WenQuanYi Micro Hei so the Chinese UI has glyphs (Apache 2.0; see `NOTICE.txt`)

There is no paid content and no map download. Buildings, curbs, crosswalks, signals, and parked cars are generated in `Streetscape`.
