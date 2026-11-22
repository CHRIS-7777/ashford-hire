// Profile page - reads and updates the account through the customer API
(function ($) {
  'use strict';
  $(function () {
    var $form = $('[data-profile-form]');
    if (!$form.length) { return; }
    var $status = $('[data-profile-status]');

    function fill(acc) {
      $form.find('[name=fullName]').val(acc.fullName);
      $form.find('[name=company]').val(acc.company);
      $form.find('[name=email]').val(acc.email);
      $form.find('[name=phone]').val(acc.phone);
      $form.find('[name=username]').val(acc.username);
      $form.find('[name=accountType]').val(acc.accountType);
    }

    $.getJSON('/api/v2/account').done(fill);

    $form.on('submit', function (e) {
      e.preventDefault();
      var body = {
        fullName: $form.find('[name=fullName]').val(),
        company: $form.find('[name=company]').val(),
        email: $form.find('[name=email]').val(),
        phone: $form.find('[name=phone]').val()
      };
      $.ajax({ url: '/api/v2/account', method: 'PUT', contentType: 'application/json', data: JSON.stringify(body) })
        .done(function (acc) {
          fill(acc);
          $status.html('<div class="alert alert-success">Profile updated.</div>');
        })
        .fail(function () {
          $status.html('<div class="alert alert-error">Your profile could not be saved. Check the fields and try again.</div>');
        });
    });
  });
})(jQuery);
