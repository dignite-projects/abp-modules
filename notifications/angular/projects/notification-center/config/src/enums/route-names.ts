/**
 * Route/menu names for the Notification Center — the value doubles as the display text's
 * localization key, resolved against the `NotificationCenter` resource defined in the module's
 * `Domain.Shared` layer. Mirrors ABP's `e<Module>RouteNames` convention (e.g. `eIdentityRouteNames`).
 */
export const enum eNotificationCenterRouteNames {
  Notifications = 'NotificationCenter::Menu:NotificationCenter',
}
