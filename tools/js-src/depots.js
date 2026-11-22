// Depot finder - loads depots from the customer API
(function ($) {
  'use strict';
  $(function () {
    var $list = $('[data-depot-list]');
    if (!$list.length) { return; }

    function esc(s) {
      return $('<div>').text(s == null ? '' : String(s)).html();
    }

    function load(region) {
      $list.html('<div class="card muted">Loading depots...</div>');
      var url = '/api/v2/depots' + (region ? '?region=' + encodeURIComponent(region) : '');
      $.getJSON(url).done(function (data) {
        if (!data.depots || !data.depots.length) {
          $list.html('<div class="card muted">No depots found in this region.</div>');
          return;
        }
        var html = '';
        $.each(data.depots, function (i, d) {
          html += '<div class="card depot"><h3>' + esc(d.name) + '</h3>' +
            '<div class="muted small">Depot code ' + esc(d.code) + '</div>' +
            '<p>' + esc(d.town) + '<br>' + esc(d.postcode) + '</p>' +
            '<p><strong>' + esc(d.phone) + '</strong><br><span class="muted small">Mon-Fri 7am-5pm, Sat 7:30am-12pm</span></p></div>';
        });
        $list.html(html);
      }).fail(function () {
        $list.html('<div class="card muted">Depots could not be loaded. Please try again.</div>');
      });
    }

    $('[data-depot-tabs] .tab').on('click', function () {
      $('[data-depot-tabs] .tab').removeClass('active');
      $(this).addClass('active');
      load($(this).attr('data-region'));
    });
    load('');
  });
})(jQuery);
