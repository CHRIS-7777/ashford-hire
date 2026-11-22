// Booking panel: live price estimate and off-hire date
(function ($, moment, Ashford) {
  'use strict';
  $(function () {
    var $panel = $('[data-booking]');
    if (!$panel.length) { return; }
    var day = parseFloat($panel.data('day-rate'));
    var week = parseFloat($panel.data('week-rate'));
    var pricing = Ashford.pricing();

    function price(days) {
      var weeks = Math.floor(days / 7);
      var rem = days % 7;
      return weeks * week + Math.min(rem * day, week);
    }

    function update() {
      var days = parseInt($('[data-days]').val(), 10) || 1;
      var start = moment($('[data-start]').val(), 'YYYY-MM-DD');
      if (start.isValid()) {
        $('[data-off-hire]').text('Off hire ' + start.clone().add(days - 1, 'days').format('dddd D MMMM') + ' (starts ' + start.fromNow() + ')');
      }
      var subtotal = price(days);
      var discount = Math.round(subtotal * (pricing.discount || 0)) / 100;
      $('[data-subtotal]').text(Ashford.money(subtotal));
      if (pricing.discount > 0) {
        $('[data-discount-row]').prop('hidden', false);
        $('[data-discount-label]').text(pricing.tier + ' discount (' + pricing.discount + '%)');
        $('[data-discount]').text('-' + Ashford.money(discount));
      }
      $('[data-total]').text(Ashford.money(Math.max(0, subtotal - discount)));
      $('[data-quote-days]').val(days);
      $('[data-quote-start]').val($('[data-start]').val());
    }

    $('[data-days], [data-start]').on('input change', update);
    update();
  });
})(jQuery, moment, window.Ashford);
