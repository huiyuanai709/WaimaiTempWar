using System.Collections.Generic;
using Godot;

namespace DeliveryTemperatureWar;

public enum PainPhase
{
    Pickup,
    Street,
    Building,
}

/// <summary>
/// One delivery-rider pain point. Spatial street set-pieces stay on the road
/// triggers; everything else is rolled from the weighted table.
/// </summary>
public sealed class PainDef
{
    public PainDef(
        string id,
        PainPhase phase,
        string title,
        string effect,
        int weight,
        bool implemented,
        float duration = 0f,
        float cooling = 0f,
        float speed = 1f,
        bool foot = false,
        float spill = 0f,
        float impulse = 0f,
        bool lid = false,
        bool food = false,
        bool spatial = false)
    {
        Id = id;
        Phase = phase;
        Title = title;
        Effect = effect;
        Weight = weight;
        Implemented = implemented;
        Duration = duration;
        Cooling = cooling;
        Speed = speed;
        Foot = foot;
        Spill = spill;
        Impulse = impulse;
        Lid = lid;
        Food = food;
        Spatial = spatial;
    }

    public string Id { get; }
    public PainPhase Phase { get; }
    public string Title { get; }
    public string Effect { get; }
    public int Weight { get; }
    public bool Implemented { get; }
    public float Duration { get; }
    public float Cooling { get; }
    public float Speed { get; }
    public bool Foot { get; }
    public float Spill { get; }
    public float Impulse { get; }
    public bool Lid { get; }
    public bool Food { get; }
    public bool Spatial { get; }
}

public partial class PhaseEvents : Node
{
    private sealed class Running
    {
        public PainDef Def = null!;
        public float Left;
    }

    public static readonly PainDef[] Catalog =
    {
        new("queue_cut", PainPhase.Pickup, "有人插队", "叫号被挤到后面，出门再堵两秒", 5, true),
        new("same_name", PainPhase.Pickup, "同名错袋", "柜台上有一只看起来也是 17 号", 4, true),
        new("merchant_wait", PainPhase.Pickup, "再等两分钟", "店员让你等，汤先开始凉", 4, true, 3.4f, 0.75f),
        new("unsealed", PainPhase.Pickup, "没封口", "饮料没盖严，洒漏变高", 3, true, spill: 0.02f, lid: true),
        new("door_bikes", PainPhase.Pickup, "门口被占", "共享单车和广告牌挡着店门", 3, true),

        new("red_light", PainPhase.Street, "红灯", "停在线前，闯了会有横车", 0, true, spatial: true),
        new("sudden_brake", PainPhase.Street, "前车急刹", "车道里一辆车突然停死", 0, true, spatial: true),
        new("ebike_cut", PainPhase.Street, "电瓶车加塞", "侧向被别了一下，得慢一点", 4, true, 2.2f, 0.12f, 0.62f, impulse: 24f),
        new("taxi_door", PainPhase.Street, "出租车开门", "车门弹开，车把被带偏", 3, true, 1.8f, 0.08f, 0.7f, impulse: 18f),
        new("construction", PainPhase.Street, "导航进围挡", "绕路，温度掉得更快", 3, true, 5f, 0.5f, 0.74f),
        new("wet_brake", PainPhase.Street, "雨天刹车湿", "制动发飘，速度上不去", 3, true, 4f, 0.06f, 0.8f),
        new("rain_gust", PainPhase.Street, "风雨", "风灌进保温箱，盖子可能被掀", 0, true, spatial: true),
        new("unanswered", PainPhase.Street, "客户不接电话", "边骑边打，心更急，凉得更快", 2, true, 3.5f, 0.4f),
        new("security_log", PainPhase.Street, "门岗登记", "访客本写半天", 2, true, 3f, 0.35f, 0.85f),
        new("dog_chase", PainPhase.Street, "狗追车", "狗贴着车蹭，箱子跟着晃", 0, true, spatial: true),
        new("reverse_ped", PainPhase.Street, "逆行行人", "对面一个人横过来", 3, true, 1.6f, 0.05f, 0.66f, impulse: 20f),
        new("puddle", PainPhase.Street, "水坑吓一跳", "车把被拽了一下，汤面倾斜", 4, true, impulse: 16f, food: true),
        new("low_battery", PainPhase.Street, "没电了", "电门变软，只能慢慢找感觉", 2, true, 6f, 0.05f, 0.6f),

        new("full_car", PainPhase.Building, "电梯满员", "这趟上不去，再等一班", 4, true),
        new("wrong_floor", PainPhase.Building, "坐过站", "下到别的层，再坐回来", 3, true, 2.4f, 0.22f),
        new("stairs", PainPhase.Building, "电梯故障", "走楼梯，体力空了会洒", 0, true, spatial: true),
        new("wrong_code", PainPhase.Building, "门禁错了", "密码不对，在门口耗着", 3, true),
        new("full_locker", PainPhase.Building, "快递柜满了", "柜门不开，还是得上门", 2, true),
        new("photo", PainPhase.Building, "要拍照留证", "放门口之前得拍一张", 3, true),
        new("address_change", PainPhase.Building, "临时改地址", "还是这栋楼，但先确认半天", 2, true, 3f, 0.35f),
        new("dark_hall", PainPhase.Building, "楼道没灯", "看不清，只能摸黑走慢", 2, true, 18f, speed: 0.78f, foot: true),
        new("pet_jump", PainPhase.Building, "宠物扑上来", "猫或狗一下子窜出来", 3, true, spill: 0.025f, food: true),
        new("neighbor", PainPhase.Building, "邻居再带一包", "门口多塞了一个小件", 2, true),

        new("tip", PainPhase.Building, "客户好评", "难得一句谢谢，这单不罚时", 1, true),
        new("buy_water", PainPhase.Street, "顺路买瓶水", "脑子里转了一下，手忙了几秒", 2, true, 4f, 0.22f, 0.88f),
    };

    private readonly List<string> _fired = new();
    private readonly List<Running> _running = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _impulse;
    private float _spill;
    private bool _lid;
    private bool _food;

    public float Cooling { get; private set; }
    public float BikeScale { get; private set; } = 1f;
    public float FootScale { get; private set; } = 1f;
    public float MountLock { get; private set; }
    public float ElevatorExtra { get; private set; }
    public float DoorExtra { get; private set; }
    public bool WrongFloor { get; private set; }
    public string Toast { get; private set; } = "";
    public float ToastLeft { get; private set; }
    public int Count => _fired.Count;

    public override void _EnterTree()
    {
        AddToGroup("pain");
    }

    public override void _Ready()
    {
        _rng.Randomize();
        if (DeliveryGame.IsAutotest())
        {
            Force("queue_cut", "same_name");
        }
        else
        {
            Roll(PainPhase.Pickup, 2);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Cooling = 0f;
        BikeScale = 1f;
        FootScale = 1f;
        for (int i = _running.Count - 1; i >= 0; i--)
        {
            Running run = _running[i];
            run.Left -= dt;
            Cooling += run.Def.Cooling;
            if (run.Def.Speed < 0.99f)
            {
                if (run.Def.Foot)
                {
                    FootScale = Mathf.Min(FootScale, run.Def.Speed);
                }
                else
                {
                    BikeScale = Mathf.Min(BikeScale, run.Def.Speed);
                }
            }

            if (run.Left <= 0f)
            {
                _running.RemoveAt(i);
            }
        }

        if (MountLock > 0f)
        {
            MountLock = Mathf.Max(0f, MountLock - dt);
        }

        if (ToastLeft > 0f)
        {
            ToastLeft -= dt;
        }
    }

    public void OnPhase(DeliveryGame.RunPhase phase)
    {
        if (DeliveryGame.IsAutotest())
        {
            if (phase == DeliveryGame.RunPhase.Ride)
            {
                Force("puddle", "wet_brake");
            }
            else if (phase == DeliveryGame.RunPhase.Indoors)
            {
                Force("full_car");
            }

            return;
        }

        if (phase == DeliveryGame.RunPhase.Ride)
        {
            Roll(PainPhase.Street, 2);
        }
        else if (phase == DeliveryGame.RunPhase.Indoors)
        {
            Roll(PainPhase.Building, 2);
        }
    }

    public bool Has(string id)
    {
        return _fired.Contains(id);
    }

    public string FiredLine()
    {
        return string.Join(",", _fired);
    }

    public void NotifyClaimed()
    {
        if (!Has("merchant_wait"))
        {
            return;
        }

        MountLock = 3.4f;
        PainDef? def = Find("merchant_wait");
        if (def != null)
        {
            Arm(def);
        }
    }

    public float ConsumeImpulse()
    {
        float value = _impulse;
        _impulse = 0f;
        return value;
    }

    public float ConsumeSpill()
    {
        float value = _spill;
        _spill = 0f;
        return value;
    }

    public bool ConsumeLid()
    {
        bool value = _lid;
        _lid = false;
        return value;
    }

    public bool ConsumeFood()
    {
        bool value = _food;
        _food = false;
        return value;
    }

    private void Force(params string[] ids)
    {
        foreach (string id in ids)
        {
            PainDef? def = Find(id);
            if (def != null)
            {
                Start(def);
            }
        }

        GD.Print("POOL " + FiredLine());
    }

    private void Roll(PainPhase phase, int count)
    {
        var pool = new List<PainDef>();
        foreach (PainDef def in Catalog)
        {
            if (def.Phase == phase && def.Implemented && !def.Spatial && def.Weight > 0 && !_fired.Contains(def.Id))
            {
                pool.Add(def);
            }
        }

        for (int n = 0; n < count && pool.Count > 0; n++)
        {
            int total = 0;
            foreach (PainDef def in pool)
            {
                total += def.Weight;
            }

            int ticket = _rng.RandiRange(1, total);
            int cursor = 0;
            int pick = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                cursor += pool[i].Weight;
                if (ticket <= cursor)
                {
                    pick = i;
                    break;
                }
            }

            Start(pool[pick]);
            pool.RemoveAt(pick);
        }

        GD.Print("POOL " + FiredLine());
    }

    private void Start(PainDef def)
    {
        if (_fired.Contains(def.Id))
        {
            return;
        }

        _fired.Add(def.Id);
        Toast = $"{def.Title}  {def.Effect}";
        ToastLeft = 3.4f;
        GD.Print($"PAIN {def.Id}");
        if (def.Id == "merchant_wait")
        {
            return;
        }

        if (def.Duration > 0f)
        {
            Arm(def);
        }

        _spill += def.Spill;
        _impulse += def.Impulse;
        _lid = _lid || def.Lid;
        _food = _food || def.Food;
        if (def.Id == "full_car")
        {
            ElevatorExtra += 3.2f;
        }
        else if (def.Id == "address_change")
        {
            ElevatorExtra += 2.5f;
        }
        else if (def.Id == "wrong_floor")
        {
            WrongFloor = true;
        }
        else if (def.Id == "wrong_code")
        {
            DoorExtra += 1.6f;
        }
        else if (def.Id == "full_locker")
        {
            DoorExtra += 2f;
        }
        else if (def.Id == "photo")
        {
            DoorExtra += 2.2f;
        }
        else if (def.Id == "neighbor")
        {
            DoorExtra += 1.8f;
        }
    }

    private void Arm(PainDef def)
    {
        _running.Add(new Running { Def = def, Left = def.Duration });
    }

    private static PainDef? Find(string id)
    {
        foreach (PainDef def in Catalog)
        {
            if (def.Id == id)
            {
                return def;
            }
        }

        return null;
    }
}
