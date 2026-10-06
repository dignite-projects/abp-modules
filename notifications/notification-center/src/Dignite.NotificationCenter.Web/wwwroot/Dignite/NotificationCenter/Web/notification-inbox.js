(function () {
    'use strict';

    // Inbox page (/NotificationCenter/Notifications). The list, tabs and paging are server-rendered; this script
    // only performs the user's actions through ABP's dynamic proxies (abp.ajax handles auth, antiforgery and error
    // display). Marking read updates the page in place — the item stays listed — while deletes and bulk actions
    // reload so paging and group counts stay authoritative. The bell's badge follows through the
    // "dignite.notificationCenter.unreadCountChanged" ABP event (see notification-center.js).
    var unreadCountChangedEvent = 'dignite.notificationCenter.unreadCountChanged';

    function inboxApi() {
        return dignite.notificationCenter.userNotification;
    }

    function inbox() {
        return document.querySelector('.dignite-notification-inbox');
    }

    function decrementUnreadCount(groupName) {
        var page = inbox();
        if (!page) {
            return;
        }

        var total = null;
        page.querySelectorAll('.dignite-inbox-unread-count').forEach(function (badge) {
            var badgeGroup = badge.getAttribute('data-group-name');
            if (badgeGroup !== '' && badgeGroup !== groupName) {
                return;
            }

            var count = Math.max(0, parseInt(badge.getAttribute('data-count') || '0', 10) - 1);
            badge.setAttribute('data-count', count);
            badge.textContent = count;
            if (count > 0) {
                badge.removeAttribute('hidden');
            } else {
                badge.setAttribute('hidden', '');
            }

            if (badgeGroup === '') {
                total = count;
            }
        });

        if (total !== null) {
            var markAll = page.querySelector('.dignite-inbox-mark-all-read');
            if (markAll && total === 0) {
                markAll.setAttribute('disabled', '');
            }
            abp.event.trigger(unreadCountChangedEvent, total);
        }
    }

    function openItem(item) {
        var notificationId = item.getAttribute('data-notification-id');
        var url = item.getAttribute('data-url');
        var navigate = function () {
            if (url) {
                window.location.href = url;
            }
        };

        if (!item.classList.contains('dignite-inbox-unread') || !notificationId
            || item.getAttribute('data-marking') === 'true') {
            navigate();
            return;
        }

        item.setAttribute('data-marking', 'true');
        inboxApi().markAsRead(notificationId).then(function () {
            item.classList.remove('dignite-inbox-unread');
            decrementUnreadCount(item.getAttribute('data-group-name'));
            navigate();
        }, function () {
            navigate();
        }).always(function () {
            item.removeAttribute('data-marking');
        });
    }

    function confirmThen(button, action) {
        abp.message.confirm(button.getAttribute('data-confirm-message')).then(function (confirmed) {
            if (!confirmed) {
                return;
            }

            button.setAttribute('disabled', '');
            action().then(function () {
                window.location.reload();
            }, function () {
                button.removeAttribute('disabled');
            });
        });
    }

    document.addEventListener('click', function (e) {
        if (!inbox()) {
            return;
        }

        var deleteButton = e.target.closest('.dignite-inbox-delete');
        if (deleteButton) {
            var item = deleteButton.closest('.dignite-inbox-item');
            confirmThen(deleteButton, function () {
                return inboxApi().delete(item.getAttribute('data-notification-id'));
            });
            return;
        }

        if (e.target.closest('.dignite-inbox-mark-all-read')) {
            confirmThen(e.target.closest('.dignite-inbox-mark-all-read'), function () {
                return inboxApi().markAllAsRead();
            });
            return;
        }

        if (e.target.closest('.dignite-inbox-delete-all-read')) {
            confirmThen(e.target.closest('.dignite-inbox-delete-all-read'), function () {
                return inboxApi().deleteAllRead();
            });
            return;
        }

        var body = e.target.closest('.dignite-inbox-item-body');
        if (body && !e.target.closest('a')) {
            openItem(body.closest('.dignite-inbox-item'));
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') {
            return;
        }

        var body = e.target.closest && e.target.closest('.dignite-inbox-item-body');
        if (body && inbox()) {
            e.preventDefault();
            openItem(body.closest('.dignite-inbox-item'));
        }
    });
})();
