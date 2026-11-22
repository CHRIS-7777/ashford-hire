// Ashford customer portal - shared helpers
// bundled by tools/build_assets.py (terser-lite)
(function (window, $) {
  'use strict';
  var Ashford = window.Ashford = window.Ashford || {};

  Ashford.money = function (n) {
    return '£' + Number(n || 0).toFixed(2);
  };

  Ashford.readCookie = function (name) {
    var parts = document.cookie ? document.cookie.split('; ') : [];
    for (var i = 0; i < parts.length; i++) {
      if (parts[i].indexOf(name + '=') === 0) {
        return decodeURIComponent(parts[i].substring(name.length + 1));
      }
    }
    return null;
  };

  // pricing profile is issued at sign-in so the on-page estimate matches checkout
  Ashford.pricing = function () {
    var raw = Ashford.readCookie('ash_pricing');
    if (!raw) { return { tier: 'retail', discount: 0 }; }
    try {
      while (raw.length % 4 !== 0) { raw += '='; }
      return JSON.parse(atob(raw));
    } catch (e) {
      return { tier: 'retail', discount: 0 };
    }
  };

  $(function () {
    $('[data-nav-toggle]').on('click', function () {
      $('.nav').toggleClass('open');
    });
    $('[data-copy]').on('click', function () {
      var input = $('[data-copy-source]')[0];
      input.select();
      document.execCommand('copy');
      $(this).text('Copied');
    });
  });
})(window, jQuery);
