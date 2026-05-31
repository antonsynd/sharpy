# Performance Benchmarks

Sharpy compiles to .NET IL via C#, delivering near-native performance while keeping Pythonic syntax.
These benchmarks compare equivalent programs across three runtimes.

## Latest Results

<div id="benchmark-table">Loading...</div>

## Performance Over Time

<canvas id="benchmark-chart" width="800" height="400"></canvas>

## Methodology

| Benchmark | What it measures |
|-----------|-----------------|
| **fibonacci** | Recursive + iterative compute (function call overhead) |
| **sorting** | Quicksort with comprehensions (allocation, recursion) |
| **string_ops** | Concatenation, case conversion, splitting (GC pressure) |
| **list_comprehensions** | Filtered + nested comprehensions (collection construction) |
| **matrix_multiply** | Nested loops, array indexing (tight numeric loops) |

Each program is semantically equivalent across all three languages. The C# version represents
idiomatic hand-written code (not hyper-optimized). Benchmarks run weekly on `ubuntu-latest` via GitHub Actions.

**Spy/Py ratio** — values below 1.0 mean Sharpy is faster than CPython.
**Spy/C# ratio** — values near 1.0 mean Sharpy has minimal overhead vs raw C#.

<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<script>
(async function() {
  const BASE = 'https://raw.githubusercontent.com/antonsynd/sharpy/dev/benchmarks/cross-language/results';

  // Load latest results
  let latest, history;
  try {
    const [latestResp, historyResp] = await Promise.all([
      fetch(`${BASE}/latest.json`),
      fetch(`${BASE}/history.json`)
    ]);
    latest = await latestResp.json();
    history = await historyResp.json();
  } catch (e) {
    document.getElementById('benchmark-table').innerHTML =
      '<p><em>Benchmark data not yet available. Results will appear after the first weekly run.</em></p>';
    return;
  }

  // Render table
  if (latest && latest.length > 0) {
    const benchmarks = {};
    latest.forEach(r => {
      if (!benchmarks[r.name]) benchmarks[r.name] = {};
      benchmarks[r.name][r.language] = r;
    });

    function fmt(r) {
      if (!r || !r.success) return 'FAIL';
      const s = r.elapsed_seconds;
      if (s < 1.0) return `${(s * 1000).toFixed(0)}ms`;
      return `${s.toFixed(2)}s`;
    }

    let html = '<table><thead><tr><th>Benchmark</th><th>Python</th><th>Sharpy</th><th>C#</th><th>Spy/Py</th><th>Spy/C#</th></tr></thead><tbody>';
    Object.keys(benchmarks).sort().forEach(name => {
      const langs = benchmarks[name];
      const py = langs['Python'] || {};
      const spy = langs['Sharpy'] || {};
      const cs = langs['C#'] || {};
      const pyT = py.success ? py.elapsed_seconds : 0;
      const spyT = spy.success ? spy.elapsed_seconds : 0;
      const csT = cs.success ? cs.elapsed_seconds : 0;
      const ratioPy = (pyT > 0 && spyT > 0) ? `${(spyT / pyT).toFixed(2)}x` : '—';
      const ratioCs = (csT > 0 && spyT > 0) ? `${(spyT / csT).toFixed(2)}x` : '—';
      html += `<tr><td><strong>${name}</strong></td><td>${fmt(py)}</td><td>${fmt(spy)}</td><td>${fmt(cs)}</td><td>${ratioPy}</td><td>${ratioCs}</td></tr>`;
    });
    html += '</tbody></table>';
    document.getElementById('benchmark-table').innerHTML = html;
  }

  // Render chart
  if (history && history.length > 1) {
    const dates = history.map(h => h.date);
    const benchNames = [...new Set(history[0].results.map(r => r.name))];
    const colors = ['#e6194b', '#3cb44b', '#4363d8', '#f58231', '#911eb4'];

    const datasets = benchNames.map((name, i) => {
      const data = history.map(h => {
        const spy = h.results.find(r => r.name === name && r.language === 'Sharpy');
        const py = h.results.find(r => r.name === name && r.language === 'Python');
        if (spy && spy.success && py && py.success && py.elapsed_seconds > 0) {
          return (spy.elapsed_seconds / py.elapsed_seconds);
        }
        return null;
      });
      return {
        label: name + ' (Spy/Py)',
        data: data,
        borderColor: colors[i % colors.length],
        backgroundColor: colors[i % colors.length] + '33',
        tension: 0.3,
        spanGaps: true
      };
    });

    new Chart(document.getElementById('benchmark-chart'), {
      type: 'line',
      data: { labels: dates, datasets: datasets },
      options: {
        responsive: true,
        plugins: {
          title: { display: true, text: 'Sharpy/Python Ratio Over Time (lower = faster)' },
          legend: { position: 'bottom' }
        },
        scales: {
          y: {
            title: { display: true, text: 'Spy/Py ratio' },
            beginAtZero: true
          },
          x: { title: { display: true, text: 'Week' } }
        }
      }
    });
  } else {
    document.getElementById('benchmark-chart').parentElement.innerHTML +=
      '<p><em>Chart will appear after 2+ weeks of data.</em></p>';
  }
})();
</script>
