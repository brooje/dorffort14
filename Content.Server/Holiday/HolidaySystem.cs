using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Holiday
{
    public sealed class HolidaySystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _configManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;

        [ViewVariables]
        private readonly List<HolidayPrototype> _currentHolidays = new();

        [ViewVariables]
        private bool _enabled = true;

        public override void Initialize()
        {
            _configManager.OnValueChanged(CCVars.HolidaysEnabled, OnHolidaysEnableChange, true);

            SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        }

        public void RefreshCurrentHolidays()
        {
            _currentHolidays.Clear();

            if (!_enabled)
            {
                RaiseLocalEvent(new HolidaysRefreshedEvent(Enumerable.Empty<HolidayPrototype>()));
                return;
            }

            var now = DateTime.Now;

            foreach (var holiday in _prototypeManager.EnumeratePrototypes<HolidayPrototype>())
            {
                if (holiday.ShouldCelebrate(now))
                {
                    _currentHolidays.Add(holiday);
                }
            }

            RaiseLocalEvent(new HolidaysRefreshedEvent(_currentHolidays));
        }

        public void DoGreet()
        {
            foreach (var holiday in _currentHolidays)
            {
                _chatManager.DispatchServerAnnouncement(holiday.Greet());
            }
        }

        public void DoCelebrate()
        {
            foreach (var holiday in _currentHolidays)
            {
                holiday.Celebrate();
            }
        }

        public IEnumerable<HolidayPrototype> GetCurrentHolidays()
        {
            return _currentHolidays;
        }

        public bool IsCurrentlyHoliday(string holiday)
        {
            if (!_prototypeManager.TryIndex(holiday, out HolidayPrototype? prototype))
                return false;

            return _currentHolidays.Contains(prototype);
        }

        private void OnHolidaysEnableChange(bool enabled)
        {
            _enabled = enabled;

            RefreshCurrentHolidays();
        }

        private void OnRunLevelChanged(GameRunLevelChangedEvent eventArgs)
        {
            if (!_enabled) return;

            switch (eventArgs.New)
            {
                case GameRunLevel.PreRoundLobby:
                    RefreshCurrentHolidays();
                    break;
                case GameRunLevel.InRound:
                    DoGreet();
                    DoCelebrate();
                    break;
                case GameRunLevel.PostRound:
                    break;
            }
        }
    }

    /// <summary>
    ///     Event for when the list of currently active holidays has been refreshed.
    /// </summary>
    public sealed class HolidaysRefreshedEvent : EntityEventArgs
    {
        public readonly IEnumerable<HolidayPrototype> Holidays;

        public HolidaysRefreshedEvent(IEnumerable<HolidayPrototype> holidays)
        {
            Holidays = holidays;
        }
    }
}
