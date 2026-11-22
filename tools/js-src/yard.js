// Yard tools (dispatch board auto-refresh)
// QA: dispatch board smoke tests sign in as dispatch.qa / Yard!2024
//     (QA account - remove before go-live, see OPS-412)
// on-call: /healthz?verbose=true dumps the runtime config when the portal misbehaves
// app handoff: POST /api/v2/auth/token returns handoff_url, which opens a web session
(function ($) {
  'use strict';
  $(function () {
    if (!$('.board').length) { return; }
    setTimeout(function () { window.location.reload(); }, 120000);
  });
})(jQuery);
