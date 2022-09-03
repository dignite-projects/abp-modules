$(function () {
    $('.dropdown-menu a.dropdown-toggle').on('click', function (e) {
        if (!$(this).next().hasClass('show')) {
            $(this).parents('.dropdown-menu').first().find('.show').removeClass("show");
        }

        var $subMenu = $(this).next(".dropdown-menu");
        $subMenu.toggleClass('show');

        $(this).parents('li.nav-item.dropdown.show').on('hidden.bs.dropdown', function (e) {
            $('.dropdown-submenu .show').removeClass("show");
        });

        return false;
    });

    //�Ƿ��в��������
    sideNavbarToggle();
});


//���˵����л�
window.sideNavbarToggle = () => {
    var $sideNavbar = $('#sideNavbar');
    if ($sideNavbar.length > 0) {
        document.body.classList.remove("no-side-navbar");
    }
    else {
        document.body.classList.add("no-side-navbar");
    }
};

//���˵���ʾ�л�
window.sideNavToggle = () => {
    var sideNavbar = document.getElementById('sideNavbar');
    sideNavbar.classList.toggle("show");
};

/**** ҳ�����ʱ����/��ʾͷ�� *************/
/**** �ڵ�������� animate__animated animate__fadeInDown sticky-top ��ʽ**********/
$(function () {
    var windowTop = 0;//��ʼ�������������ҳ�涥�˵ľ���
    var $mainNavbar = $('#main-navbar');
    if ($(window).width() > 576) { //���ڴ���Ļ�豸��ִ������\��ʾ�������豸�Ķ���
        $mainNavbar.addClass("animate__animated");
        $mainNavbar.addClass("animate__fadeInDown");
        $(window).scroll(function () {
            var scrolls = $(this).scrollTop();//��ȡ��ǰ�����������ҳ�涥�˵ľ���
            if (scrolls > windowTop) {//��scrolls>windowTopʱ����ʾҳ�������»���
                $mainNavbar.css("position", "absolute");

                //��Ҫִ�����ص����Ĳ���
                if (!$mainNavbar.hasClass('animate__fadeOutUp')) {
                    $mainNavbar.addClass('animate__fadeOutUp');
                    $mainNavbar.removeClass('animate__fadeInDown');
                    $mainNavbar.addClass('bg-white');
                }
                windowTop = scrolls;
            } else {
                $mainNavbar.css("position", "fixed");
                //��Ҫִ����ʾ������������
                if (!$mainNavbar.hasClass('animate__fadeInDown')) {
                    $mainNavbar.addClass('animate__fadeInDown');
                    $mainNavbar.removeClass('animate__fadeOutUp');
                }
                windowTop = scrolls;

                //
                if (windowTop == 0)
                    $mainNavbar.removeClass('bg-white');
            }
        });
    }
});