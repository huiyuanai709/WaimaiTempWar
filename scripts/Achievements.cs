using System.Collections.Generic;
using Godot;

namespace DeliveryTemperatureWar;

public sealed class AchievementEntry
{
    public AchievementEntry(string id, string nameZh, string nameEn, string detail)
    {
        Id = id;
        NameZh = nameZh;
        NameEn = nameEn;
        Detail = detail;
    }

    public string Id { get; }
    public string NameZh { get; }
    public string NameEn { get; }
    public string Detail { get; }
}

public partial class Achievements : Node
{
    public const string FilePath = "user://achievements.cfg";

    public static readonly AchievementEntry[] Catalog =
    {
        new("hot_delivery", "首单热达", "First hot drop", "Finish a delivery at or above 60°C without spilling out."),
        new("zero_spill", "滴汤不洒", "Zero spill", "Finish a delivery with under 1% spilled."),
        new("hard_brake", "急刹不碎", "Clean hard brake", "Survive the sudden-brake event with crush still under 8%."),
        new("triple_event", "一路三事", "Three events", "Trigger three different random events in one run."),
        new("clean_pickup", "号对袋对", "Right bag", "Take the bag that matches your ticket without grabbing a wrong one."),
        new("stair_hot", "爬楼还热", "Hot stairs", "Deliver by the stairs while the soup is still at least 60°C."),
        new("elevator_clean", "电梯不洒", "Clean elevator", "Ride the elevator and finish with under 1% spilled."),
        new("rain_dry", "雨天不洒", "Dry rain", "Finish under 1% spilled after a rain gust."),
        new("pain_hot", "一单多灾", "Hot despite it", "Deliver hot after at least three pain events."),
    };

    private readonly HashSet<string> _unlocked = new();
    private ConfigFile _cfg = new();

    [Signal]
    public delegate void UnlockedEventHandler(string id);

    public override void _EnterTree()
    {
        AddToGroup("achievements");
    }

    public override void _Ready()
    {
        if (DeliveryGame.IsAutotest())
        {
            _cfg = new ConfigFile();
            _cfg.Save(FilePath);
            return;
        }

        Load();
    }

    public bool IsUnlocked(string id)
    {
        return _unlocked.Contains(id);
    }

    public bool Unlock(string id)
    {
        if (!_unlocked.Add(id))
        {
            return false;
        }

        _cfg.SetValue("achievements", id, true);
        Error err = _cfg.Save(FilePath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Could not save achievements ({err}).");
        }

        GD.Print($"ACHIEVEMENT {id}");
        EmitSignal(SignalName.Unlocked, id);
        return true;
    }

    public void Reset()
    {
        _unlocked.Clear();
        _cfg = new ConfigFile();
        _cfg.Save(FilePath);
    }

    private void Load()
    {
        Error err = _cfg.Load(FilePath);
        if (err != Error.Ok)
        {
            return;
        }

        foreach (AchievementEntry entry in Catalog)
        {
            if (_cfg.GetValue("achievements", entry.Id, false).AsBool())
            {
                _unlocked.Add(entry.Id);
            }
        }
    }
}
