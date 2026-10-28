# Delivery Temperature War

外卖温度战争：你是电动车外卖骑手。先在挤满人的面馆把对的那袋拿走，再骑过一条短街区，最后进楼，电梯好就坐电梯，电梯坏了就爬楼梯。汤从取到手就开始凉。

You are an e-bike courier. Pick up the right bag in a crowded shop, ride one short block, then go inside the building. A working elevator is a wait. A broken one is stairs, stamina, and more spill. The soup starts cooling once it is in your hands.

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

That walks the shop, rides the block, takes the elevator, prints `AUTOTEST_JSON`, and exits 0 when the forced pain events fire, the cooler sags, and a hot delivery is saved.

```bash
WAIMAI_STAIRS=1 godot --headless --path . -- --autotest
```

Same loop, but the elevator is broken and the run must stay hot on the stairs.

Force street set-pieces with:

```bash
WAIMAI_EVENTS=red,rain,gate godot --path .
```

Names: `red`, `brake`, `dog`, `rain`, `elevator`, `gate`. `elevator` means the building lift works. `gate` means it is broken and you take the stairs. `WAIMAI_ELEVATOR=1` forces the lift. `WAIMAI_STAIRS=1` forces the stairs.

## Controls

On foot (shop and building) W A S D walks relative to the camera. On the bike the same keys drive.

| Key | On foot | On the bike |
| --- | --- | --- |
| W / Up | Walk forward | Throttle |
| S / Down | Walk back | Brake, then reverse |
| A / D or arrows | Strafe / turn | Steer |
| E | Take a bag, mount, call the elevator, deliver | |
| Space | | Handbrake |
| F | | Slam the lid shut |
| 1 / 2 / 3 | 胖咕嘎 / 瘦咕嘎 / Doro | Same skins, sitting on the bike |
| Q | Next rider skin | Next rider skin |
| Tab | Achievement list | Achievement list |
| R | Restart | Restart |
| F9 | Clear saved achievements | Clear saved achievements |

The last skin is saved in `user://rider.cfg`. Headless runs always start as 胖咕嘎 and print `RIDER_SKIN=FatGuga`.

## Riders

The courier and the shop queue are original procedural mascots made in C# from spheres and boxes. No model pack is vendored, and nothing here is a Nikke, Arknights, or other official mesh.

- **胖咕嘎 FatGuga** — round black-and-white penguin, short flippers, bean eyes, a tiny visor.
- **瘦咕嘎 ThinGuga** — the same palette stretched tall, with a long body and a small belly.
- **Doro** — a soft pink and cream round critter with floppy ears and a simple blonde bob. The name is the meme nickname only; the mesh is original.

Shop customers reuse the fat and thin penguins at a smaller scale. Press 1, 2, or 3 to switch who you are playing. The parked bike shows the skin once you mount.

## How a run works

1. **到店取餐** — You start in the noodle shop. Your ticket is **17**. Three rigid bags sit on the counter. A queue rail and pacing customers block the straight line; squeeze along the left wall. Press **E** on the bag whose label is the hot soup. A same-name lookalike can be labeled 17 and is the wrong order: taking it sets the wrong-bag flag and you have to leave it. The clock starts when the right bag is in your hand. Walk out and press **E** at the bike to load the cooler.
2. **路面** — Ride to the building. The soup cools from time, speed, wind, an open lid, spill, and crush. You win only if it is still **at least 60°C** and less than **50%** spilled when you hand it over at unit 302.
3. **进楼** — The green pad is where you dismount (**E**). Inside, either call the elevator or, if it is broken, climb the ramp. Stairs spend stamina; emptying it spills the soup. Rest on the ramp to recover. The apartment door finishes the order.

The HUD shows temperature, spill, crush, ETA, speed, and a toast for each pain event (`标题  效果`).

### Street set-pieces (placed on the road)

Each run arms two of these, plus elevator-or-stairs for the building:

- **红灯 Red light** — stop behind the line. Riding through spawns cross traffic.
- **前车急刹 Sudden brake** — a car stops in your lane. Surviving with almost no new crush unlocks an achievement.
- **狗追车 Dog chase** — a dog paces you and shoves the bike.
- **风雨 Rain gust** — side wind, faster cooling, and a shove on the lid.

### Pain-event pool

`PhaseEvents` rolls **two weighted events per phase** (pickup, street, building). The headless autotest forces `queue_cut`, `same_name`, `puddle`, `wet_brake`, and `full_car` so the pool is deterministic. Everything below is implemented as time, cooling, speed, spill, impulse, or an interact wait. There is no separate minigame scene for “buy water” or the neighbor’s parcel: those are a slowdown and an extra wait at the door.

| Id | Phase | What it does |
| --- | --- | --- |
| `queue_cut` | Pickup | Call order is one number further behind, then the door is blocked for 2s after you have the bag |
| `same_name` | Pickup | A wrong bag is labeled like ticket 17. Taking it fails the clean-pickup achievement |
| `merchant_wait` | Pickup | After the claim, you cannot mount for a few seconds while cooling is faster |
| `unsealed` | Pickup | Immediate spill and a lid nudge once you are riding |
| `door_bikes` | Pickup | Shared bike and a signboard narrow the shop door |
| `ebike_cut` | Street | Side impulse and a short slow |
| `taxi_door` | Street | Side impulse and a short slow |
| `construction` | Street | Detour: slower bike and faster cooling |
| `wet_brake` | Street | Top speed drops |
| `unanswered` | Street | Faster cooling while you “keep calling” |
| `security_log` | Street | Slower bike and faster cooling |
| `reverse_ped` | Street | Side impulse and a short slow |
| `puddle` | Street | Handle yank: lateral impulse and a tilt on the meal |
| `low_battery` | Street | Forced slow |
| `buy_water` | Street | Distraction: slower and a bit of extra cooling. No shop interior |
| `full_car` | Building | Elevator call waits an extra cycle |
| `wrong_floor` | Building | Cab pauses at the top (“you passed the floor”) |
| `wrong_code` | Building | Extra seconds at the apartment door |
| `full_locker` | Building | Locker is full, so the door wait is longer |
| `photo` | Building | Timed photo proof before the handoff |
| `address_change` | Building | Extra elevator wait and cooling |
| `dark_hall` | Building | Walking is slower |
| `pet_jump` | Building | One-shot spill and a food nudge |
| `neighbor` | Building | Extra wait at the door for the extra parcel |
| `tip` | Building | Rare praise toast, no time penalty |
| `stairs` | Building | Not rolled. Chosen when the run’s building flag is the broken elevator |

Red light, sudden brake, rain, and the dog are the spatial set-pieces above, not a second roll.

## Achievements

Saved with `ConfigFile` at `user://achievements.cfg` (on Linux that is typically `~/.local/share/godot/app_userdata/Delivery Temperature War/achievements.cfg`).

| Id | Name | How |
| --- | --- | --- |
| `hot_delivery` | 首单热达 | Deliver at or above 60°C without a spill-out |
| `zero_spill` | 滴汤不洒 | Finish with under 1% spilled |
| `hard_brake` | 急刹不碎 | Get the sudden-brake event and keep new crush under 8% |
| `triple_event` | 一路三事 | Trigger three different street/arrival events in one run |
| `clean_pickup` | 号对袋对 | Take the matching bag and never grab a wrong one |
| `stair_hot` | 爬楼还热 | Deliver by the stairs at or above 60°C |
| `elevator_clean` | 电梯不洒 | Ride the elevator and finish under 1% spilled |
| `rain_dry` | 雨天不洒 | Finish under 1% spilled after the rain gust |
| `pain_hot` | 一单多灾 | Deliver hot after at least three pain events |

Tab opens the list. Unlocking shows a toast. F9 wipes the file.

## Layout

- `scenes/Main.tscn` — chase camera, lights, and the nodes the scripts drive
- `scripts/` — C# gameplay only (shop, bike, building, spring cooler, pain-event pool, temperature, achievements, HUD, street)
- `assets/fonts/` — WenQuanYi Micro Hei so the Chinese UI has glyphs (Apache 2.0; see `NOTICE.txt`)

There is no paid content and no map download. Buildings, curbs, crosswalks, signals, and parked cars are generated in `Streetscape`.
