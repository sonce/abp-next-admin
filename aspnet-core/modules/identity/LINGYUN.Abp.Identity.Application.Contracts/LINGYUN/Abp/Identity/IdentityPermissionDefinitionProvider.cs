﻿using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Localization;
using Volo.Abp.Localization;

namespace LINGYUN.Abp.Identity
{
    public class IdentityPermissionDefinitionProvider : PermissionDefinitionProvider
    {
        public override void Define(IPermissionDefinitionContext context)
        {
            var identityGroup = context.GetGroupOrNull(Volo.Abp.Identity.IdentityPermissions.GroupName);
            if (identityGroup != null)
            {
                var userPermission = identityGroup.GetPermissionOrNull(Volo.Abp.Identity.IdentityPermissions.Users.Default);
                if (userPermission != null)
                {
                    userPermission.AddChild(IdentityPermissions.Users.ManageOrganizationUnits, L("Permission:ManageOrganizationUnits"));
                }

                var rolePermission = identityGroup.GetPermissionOrNull(Volo.Abp.Identity.IdentityPermissions.Roles.Default);
                if (rolePermission != null)
                {
                    rolePermission.AddChild(IdentityPermissions.Roles.ManageOrganizationUnits, L("Permission:ManageOrganizationUnits"));
                }

                var origanizationUnitPermission = identityGroup.AddPermission(IdentityPermissions.OrganizationUnits.Default, L("Permission:OrganizationUnitManagement"));
                origanizationUnitPermission.AddChild(IdentityPermissions.OrganizationUnits.Create, L("Permission:Create"));
                origanizationUnitPermission.AddChild(IdentityPermissions.OrganizationUnits.Update, L("Permission:Edit"));
                origanizationUnitPermission.AddChild(IdentityPermissions.OrganizationUnits.Delete, L("Permission:Delete"));
                origanizationUnitPermission.AddChild(IdentityPermissions.OrganizationUnits.ManageRoles, L("Permission:ChangeRoles"));
                origanizationUnitPermission.AddChild(IdentityPermissions.OrganizationUnits.ManageUsers, L("Permission:ChangeUsers"));
            }
        }

        private static LocalizableString L(string name)
        {
            return LocalizableString.Create<IdentityResource>(name);
        }
    }
}
