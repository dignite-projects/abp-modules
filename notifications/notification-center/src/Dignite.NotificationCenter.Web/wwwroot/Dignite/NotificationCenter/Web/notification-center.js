(function () {
    'use strict';

    // ABP's dynamic JavaScript service proxies for the NotificationCenter API. They're generated at
    // /Abp/ServiceProxyScript and included on every ABP MVC page by default, so we call the application
    // services directly instead of hand-writing fetch(): abp.ajax handles antiforgery, authentication and
    // error reporting, and this surface auto-tracks the C# app services (no URLs to keep in sync). The inbox
    // and subscriptions are two controllers, so there are two proxy objects (paths follow the controller names).
    function inboxApi() {
        return dignite.abp.notificationCenter.userNotification;
    }

    function subscriptionApi() {
        return dignite.abp.notificationCenter.notificationSubscription;
    }

    function refreshDropdown(bell) {
        if (!bell || bell.getAttribute('data-refreshing-dropdown') === 'true') {
            return;
        }

        var url = bell.getAttribute('data-dropdown-url');
        var list = bell.querySelector('.dignite-notification-list');
        if (!url || !list || typeof abp === 'undefined' || !abp.ajax) {
            return;
        }

        var currentContent = list.querySelector('.dignite-notification-list-content');
        if (currentContent) {
            currentContent.setAttribute('aria-busy', 'true');
        }
        bell.setAttribute('data-refreshing-dropdown', 'true');

        var clearRefreshing = function () {
            bell.removeAttribute('data-refreshing-dropdown');
            var content = list.querySelector('.dignite-notification-list-content');
            if (content) {
                content.removeAttribute('aria-busy');
            }
        };

        abp.ajax({
            url: url,
            type: 'GET',
            dataType: 'html'
        }).then(function (html) {
            var temp = document.createElement('div');
            temp.innerHTML = (html || '').trim();

            var nextContent = temp.querySelector('.dignite-notification-list-content') || temp.firstElementChild;
            if (!nextContent) {
                clearRefreshing();
                return;
            }

            currentContent = list.querySelector('.dignite-notification-list-content');
            if (currentContent) {
                currentContent.replaceWith(nextContent);
            } else {
                list.innerHTML = '';
                list.appendChild(nextContent);
            }

            var unreadCount = parseInt(nextContent.getAttribute('data-unread-count') || '0', 10);
            if (!isNaN(unreadCount)) {
                setBadgeCount(unreadCount);
            }

            clearRefreshing();
        }, clearRefreshing);
    }

    document.addEventListener('show.bs.dropdown', function (e) {
        refreshDropdown(e.target.closest('.dignite-notification-bell'));
    });

    // ---- click handling (refresh dropdown, mark-as-read, mark-all, follow entity link) ----
    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('.dignite-notification-toggle');
        if (toggle) {
            refreshDropdown(toggle.closest('.dignite-notification-bell'));
        }

        var item = e.target.closest('.dignite-notification-item');
        if (item) {
            var notificationId = item.getAttribute('data-notification-id');
            var hasLink = item.getAttribute('data-has-link') === 'true';
            var href = item.getAttribute('href');
            e.preventDefault();
            if (notificationId) {
                inboxApi().markAsRead(notificationId).then(function () {
                    setBadgeCount(getBadgeCount() - 1);
                    item.classList.remove('dignite-notification-unread');
                    if (hasLink && href) {
                        window.location.href = href;
                    }
                }, function () {
                    if (hasLink && href) {
                        window.location.href = href;
                    }
                });
                return;
            }
            if (hasLink && href) {
                window.location.href = href;
            }
            return;
        }

        var markAllLink = e.target.closest('.dignite-notification-mark-all-read');
        if (markAllLink) {
            e.preventDefault();
            inboxApi().markAllAsRead().then(function () {
                setBadgeCount(0);
                var bell = markAllLink.closest('.dignite-notification-bell');
                if (bell) {
                    bell.querySelectorAll('.dignite-notification-item').forEach(function (item) {
                        item.classList.remove('dignite-notification-unread');
                    });
                }
            });
        }
    });

    // ---- subscription toggles ----
    document.addEventListener('change', function (e) {
        var toggle = e.target.closest('.dignite-subscription-toggle');
        if (!toggle) {
            return;
        }

        var name = toggle.getAttribute('data-notification-name');
        var entityTypeName = toggle.getAttribute('data-entity-type-name') || null;
        var entityId = toggle.getAttribute('data-entity-id') || null;
        var scope = {
            notificationName: name,
            entityTypeName: entityTypeName,
            entityId: entityId
        };
        var request = toggle.checked ? subscriptionApi().subscribe(scope) : subscriptionApi().unsubscribe(scope);
        // abp.ajax already surfaces the error to the user; just revert the checkbox if the call failed.
        request.then(null, function () {
            toggle.checked = !toggle.checked;
        });
    });

    // ---- unread badge helpers ----
    function badgeEl() {
        return document.querySelector('.dignite-notification-badge');
    }

    function getBadgeCount() {
        var el = badgeEl();
        return el ? parseInt(el.getAttribute('data-count') || '0', 10) : 0;
    }

    function setBadgeCount(count) {
        var el = badgeEl();
        if (!el) {
            return;
        }
        count = Math.max(0, count);
        el.setAttribute('data-count', count);
        el.textContent = count;
        if (count > 0) {
            el.removeAttribute('hidden');
        } else {
            el.setAttribute('hidden', '');
        }
    }

    // ---- real-time receive over ABP SignalR (optional, degrades gracefully) ----
    // The server-side Notifier (Dignite.Abp.Notifications.SignalR) pushes a per-recipient NotificationPayload
    // (recipient list already stripped, per notifications-invariants.md §4) via the strongly-typed client method
    // "ReceiveNotification". We only nudge the unread badge + flag the bell here; the authoritative, fully
    // rendered list (localized display name, custom per-type view components, entity links) is server-rendered
    // on the next open/refresh — so we never duplicate server rendering (or its localization) on the client.
    function connectSignalR() {
        if (typeof signalR === 'undefined') {
            return; // @microsoft/signalr not loaded by the host — bell stays non-live, still fully usable.
        }
        var bell = document.querySelector('.dignite-notification-bell');
        var hubUrl = bell ? bell.getAttribute('data-signalr-hub-url') : null;
        if (!hubUrl) {
            return;
        }

        var connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveNotification', function () {
            setBadgeCount(getBadgeCount() + 1);
            if (bell) {
                bell.classList.add('dignite-notification-pulse');
                setTimeout(function () { bell.classList.remove('dignite-notification-pulse'); }, 1000);
            }
        });

        connection.start().catch(function (err) {
            if (window.console) {
                console.warn('Notification hub connection failed; bell will not update live.', err);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', connectSignalR);
    } else {
        connectSignalR();
    }
})();
