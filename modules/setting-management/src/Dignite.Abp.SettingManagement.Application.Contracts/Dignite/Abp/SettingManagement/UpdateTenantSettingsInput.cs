﻿using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Dignite.Abp.SettingManagement
{
    public class UpdateTenantSettingsInput : UpdateSettingsInput
    {

        public override IReadOnlyList<SettingDto> GetSettings(ValidationContext validationContext)
        {
            var settingsAppService = validationContext.GetRequiredService<ITenantSettingsAppService>();
            var settingNavigations = settingsAppService.GetAllAsync().Result;
            return settingNavigations.Items.Single(i => i.Name == NavigationName).Settings;
        }
    }
}
