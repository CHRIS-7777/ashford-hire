(function () {
  'use strict';
  var amber = '#f5a524';
  var palette = ['#f5a524', '#2e6db4', '#1f8a5b', '#c2413a', '#7a5cc2', '#5b6b78'];
  document.querySelectorAll('canvas[data-chart]').forEach(function (el) {
    var series = JSON.parse(el.getAttribute('data-series') || '[]');
    var type = el.getAttribute('data-chart');
    new Chart(el, {
      type: type,
      data: {
        labels: series.map(function (p) { return p.label; }),
        datasets: [{
          data: series.map(function (p) { return p.value; }),
          backgroundColor: type === 'bar' ? amber : palette,
          borderColor: type === 'bar' ? amber : '#fff',
          borderRadius: type === 'bar' ? 4 : 0
        }]
      },
      options: {
        plugins: { legend: { display: type !== 'bar', position: 'bottom' } },
        scales: type === 'bar'
          ? { y: { beginAtZero: true, ticks: { callback: function (v) { return '£' + v; } } }, x: { grid: { display: false } } }
          : {}
      }
    });
  });
})();
