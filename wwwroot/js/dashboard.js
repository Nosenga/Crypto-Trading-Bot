// wwwroot/js/dashboard.js
document.addEventListener('DOMContentLoaded', function () {
    // SignalR Connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/tradingHub')
        .withAutomaticReconnect()
        .build();

    connection.start()
        .then(() => console.log("[SignalR] Connected successfully!"))
        .catch(err => console.error("[SignalR] Connection failed: ", err));

    // Listen for market updates
    connection.on('MarketUpdate', function (prices) {
        Object.keys(prices).forEach(symbol => {
            const priceElements = document.querySelectorAll(
                `[data-symbol="${symbol}"]`
            );
            priceElements.forEach(el => {
                el.textContent = '$' + parseFloat(prices[symbol])
                    .toLocaleString('en-US', {
                        minimumFractionDigits: 2,
                        maximumFractionDigits: 2
                    });
            });
        });

        document.getElementById('lastUpdate').textContent =
            'Last update: ' + new Date().toLocaleTimeString();
    });

    // Portfolio chart
    initEquityChart();
});

let equityChart;

function initEquityChart() {
    const ctx = document.getElementById('equityChart');
    if (!ctx) return;

    // Generate sample data
    const labels = [];
    const data = [];
    let value = 100000;

    for (let i = 0; i < 30; i++) {
        const date = new Date();
        date.setDate(date.getDate() - 30 + i);
        labels.push(date.toLocaleDateString('en-US', {
            month: 'short', day: 'numeric'
        }));
        value += (Math.random() - 0.45) * 2000;
        data.push(Math.round(value));
    }

    equityChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Portfolio Value',
                data: data,
                borderColor: '#5ce1ff',
                backgroundColor: createGradient(ctx),
                fill: true,
                tension: 0.4,
                pointRadius: 0,
                pointHoverRadius: 6,
                pointHoverBackgroundColor: '#5ce1ff',
                borderWidth: 2.5
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                intersect: false,
                mode: 'index'
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(20,46,68,0.85)',
                    titleColor: '#f6ede4',
                    bodyColor: '#f6ede4',
                    borderColor: 'rgba(255,255,255,0.18)',
                    borderWidth: 1,
                    callbacks: {
                        label: function (context) {
                            return '$' + context.parsed.y.toLocaleString();
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: 'rgba(255,255,255,0.08)' },
                    ticks: { color: '#8fb2c7', maxTicksLimit: 8 }
                },
                y: {
                    grid: { color: 'rgba(255,255,255,0.08)' },
                    ticks: {
                        color: '#8fb2c7',
                        callback: function (v) {
                            return '$' + (v / 1000).toFixed(0) + 'k';
                        }
                    }
                }
            }
        }
    });
}

function createGradient(ctx) {
    const canvas = ctx.getContext ? ctx : ctx.canvas;
    const context = canvas.getContext ? canvas.getContext('2d') : ctx;
    try {
        const gradient = context.createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, 'rgba(92, 225, 255, 0.32)');
        gradient.addColorStop(1, 'rgba(92, 225, 255, 0.0)');
        return gradient;
    } catch {
        return 'rgba(92, 225, 255, 0.12)';
    }
}

function updateChart(range) {
    // Update chart based on time range
    if (equityChart) {
        // Simulate different data ranges
        const points = range === '1W' ? 7 : range === '1M' ? 30 : 90;
        const labels = [];
        const data = [];
        let value = 100000;

        for (let i = 0; i < points; i++) {
            const date = new Date();
            date.setDate(date.getDate() - points + i);
            labels.push(date.toLocaleDateString('en-US', {
                month: 'short', day: 'numeric'
            }));
            value += (Math.random() - 0.45) * 1500;
            data.push(Math.round(value));
        }

        equityChart.data.labels = labels;
        equityChart.data.datasets[0].data = data;
        equityChart.update();
    }
}