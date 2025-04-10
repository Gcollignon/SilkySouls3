﻿using System;
using System.Collections.Generic;
using System.Linq;
using H.Hooks;
using SilkySouls3.Services;
using SilkySouls3.Utilities;

namespace SilkySouls3.ViewModels
{
    public class SettingsViewModel
    {
        private bool _isLoaded;
        
        private string _currentSettingHotkeyId;
        private LowLevelKeyboardHook _tempHook;
        private Keys _currentKeys;
        
        
        private readonly Dictionary<string, Action<string>> _propertySetters;
        
        private readonly SettingsService _settingsService;
        private readonly HotkeyManager _hotkeyManager;

        public SettingsViewModel(SettingsService settingsService, HotkeyManager hotkeyManager)
        {
            _settingsService = settingsService;
            _hotkeyManager = hotkeyManager;

            // RegisterHotkeys();
            //
            // _propertySetters = new Dictionary<string, Action<string>>
            // {
            //     { "SavePos1", text => SavePos1HotkeyText = text },
            //     { "SavePos2", text => SavePos2HotkeyText = text },
            //     { "RestorePos1", text => RestorePos1HotkeyText = text },
            //     { "RestorePos2", text => RestorePos2HotkeyText = text },
            //     { "RTSR", text => RtsrHotkeyText = text },
            //     { "NoDeath", text => NoDeathHotkeyText = text },
            //     { "OneShot", text => OneShotHotkeyText = text },
            //     { "RestoreSpellCasts", text => RestoreSpellCastsHotkeyText = text },
            //     { "ToggleSpeed", text => ToggleSpeedHotkeyText = text },
            //     { "IncreaseSpeed", text => IncreaseSpeedHotkeyText = text },
            //     { "DecreaseSpeed", text => DecreaseSpeedHotkeyText = text },
            //     { "NoClip", text => NoClipHotkeyText = text },
            //     { "DisableTargetAi", text => DisableTargetAiHotkeyText = text },
            //     { "FreezeHp", text => FreezeHpHotkeyText = text },
            //     { "Quitout", text => QuitoutHotkeyText = text },
            //     { "DisableAi", text => DisableAiHotkeyText = text },
            //     { "AllNoDeath", text => AllNoDeathHotkeyText = text },
            //     { "AllNoDamage", text => AllNoDamageHotkeyText = text },
            //     { "IncreaseTargetSpeed", text => IncreaseTargetSpeedHotkeyText = text },
            //     { "DecreaseTargetSpeed", text => DecreaseTargetSpeedHotkeyText = text },
            //     { "EnableTargetOptions", text => EnableTargetOptionsHotkeyText = text },
            //     { "ShowAllResistances", text => ShowAllResistancesHotkeyText = text },
            // };
            //
            // LoadHotkeyDisplays();
        }
        
        
        private void LoadHotkeyDisplays()
        {
            foreach (var entry in _propertySetters)
            {
                string actionId = entry.Key;
                Action<string> setter = entry.Value;

                setter(GetHotkeyDisplayText(actionId));
            }
        }
        
        private string GetHotkeyDisplayText(string actionId)
        {
            Keys keys = _hotkeyManager.GetHotkey(actionId);
            return keys != null && keys.Values.ToArray().Length > 0 ? string.Join(" + ", keys) : "None";
        }


        public void StartSettingHotkey(string actionId)
        {
            if (_currentSettingHotkeyId != null &&
                _propertySetters.TryGetValue(_currentSettingHotkeyId, out var prevSetter))
            {
                prevSetter(GetHotkeyDisplayText(_currentSettingHotkeyId));
            }

            _currentSettingHotkeyId = actionId;

            if (_propertySetters.TryGetValue(actionId, out var setter))
            {
                setter("Press keys...");
            }

            _tempHook = new LowLevelKeyboardHook();
            _tempHook.IsExtendedMode = true;
            _tempHook.Down += TempHook_Down;
            _tempHook.Start();
        }

        private void TempHook_Down(object sender, KeyboardEventArgs e)
        {
            if (_currentSettingHotkeyId == null || e.Keys.IsEmpty)
                return;

            try
            {
                bool containsEnter = e.Keys.Values.Contains(Key.Enter) || e.Keys.Values.Contains(Key.Return);

                if (containsEnter && _currentKeys != null)
                {
                    _hotkeyManager.SetHotkey(_currentSettingHotkeyId, _currentKeys);
                    StopSettingHotkey();
                    e.IsHandled = true;
                    return;
                }

                if (e.Keys.Values.Contains(Key.Escape))
                {
                    CancelSettingHotkey();
                    e.IsHandled = true;
                    return;
                }

                if (containsEnter)
                {
                    e.IsHandled = true;
                    return;
                }

                if (e.Keys.IsEmpty)
                    return;

                _currentKeys = e.Keys;

                if (_propertySetters.TryGetValue(_currentSettingHotkeyId, out var setter))
                {
                    string keyText = e.Keys.ToString();
                    setter(keyText);
                }
            }
            catch (Exception ex)
            {
                if (_propertySetters.TryGetValue(_currentSettingHotkeyId, out var setter))
                {
                    setter("Error: Invalid key combination");
                }
            }

            e.IsHandled = true;
        }

        private void StopSettingHotkey()
        {
            if (_tempHook != null)
            {
                _tempHook.Down -= TempHook_Down;
                _tempHook.Dispose();
                _tempHook = null;
            }

            _currentSettingHotkeyId = null;
            _currentKeys = null;
        }
        
        public void ConfirmHotkey()
        {
            
            var currentSettingHotkeyId = _currentSettingHotkeyId;
            var currentKeys = _currentKeys;
            if (currentSettingHotkeyId == null || currentKeys == null || currentKeys.IsEmpty)
            {
                CancelSettingHotkey();
                return;
            }

            HandleExistingHotkey(currentKeys);
            SetNewHotkey(currentSettingHotkeyId, currentKeys);

            StopSettingHotkey();
        }

        private void HandleExistingHotkey(Keys currentKeys)
        {
            string existingHotkeyId = _hotkeyManager.GetActionIdByKeys(currentKeys);
            if (string.IsNullOrEmpty(existingHotkeyId)) return;
            
            _hotkeyManager.ClearHotkey(existingHotkeyId);
            if (_propertySetters.TryGetValue(existingHotkeyId, out var oldSetter))
            {
                oldSetter("None");
            }
        }
        
        private void SetNewHotkey(string currentSettingHotkeyId, Keys currentKeys)
        {
            _hotkeyManager.SetHotkey(currentSettingHotkeyId, currentKeys);
            
            if (_propertySetters.TryGetValue(currentSettingHotkeyId, out var setter))
            {
                setter(new Keys(currentKeys.Values.ToArray()).ToString());
            }
        }
        
        public void CancelSettingHotkey()
        {
            if (_currentSettingHotkeyId != null &&
                _propertySetters.TryGetValue(_currentSettingHotkeyId, out var setter))
            {
                setter("None");
                _hotkeyManager.SetHotkey(_currentSettingHotkeyId, new Keys());
            }

            StopSettingHotkey();
        }

        public void ApplyStartUpOptions()
        {
        }
    }
}