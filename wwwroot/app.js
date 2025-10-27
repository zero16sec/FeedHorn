const API_BASE = '/api/monitoredurls';
const SPEEDTEST_API = '/api/speedtest';
const SSL_API = '/api/sslcertificate';
let charts = {};
let urlsData = {}; // Store full URL data for filtering
let speedTestData = []; // Store speed test data for filtering
let sslData = []; // Store SSL certificate data
let speedTestCharts = {}; // Store speed test chart instances
let refreshInterval;
let currentTab = 'urlMonitoring';
let showGraphDots = false; // Global toggle for graph dots (default: hidden)

// Initialize app
document.addEventListener('DOMContentLoaded', () => {
    loadUrls();
    loadSpeedTests();
    loadSslCertificates();
    startAutoRefresh();
    setupEventListeners();
    setupTabSwitching();
    loadDarkModePreference();
});

function setupEventListeners() {
    document.getElementById('addUrlBtn').addEventListener('click', () => {
        if (currentTab === 'ssl') {
            openSslModal();
        } else {
            openModal();
        }
    });
    document.getElementById('urlForm').addEventListener('submit', handleSubmit);
    document.getElementById('checkType').addEventListener('change', handleCheckTypeChange);
    document.getElementById('sslForm').addEventListener('submit', handleSslSubmit);
}

function handleCheckTypeChange() {
    const checkType = document.getElementById('checkType').value;
    const urlInput = document.getElementById('urlInput');
    const urlHelp = document.getElementById('urlHelp');

    if (checkType === '0') { // HTTP Check
        urlInput.placeholder = 'https://example.com';
        urlHelp.textContent = 'Enter a valid HTTP/HTTPS URL';
        urlInput.type = 'url';
    } else if (checkType === '1') { // Ping Check
        urlInput.placeholder = 'example.com or 192.168.1.1';
        urlHelp.textContent = 'Enter a hostname, IP address, or URL';
        urlInput.type = 'text';
    }
}

function startAutoRefresh() {
    // Refresh every 5 minutes (matches backend monitoring interval)
    refreshInterval = setInterval(() => {
        if (currentTab === 'urlMonitoring') {
            loadUrls();
        } else if (currentTab === 'speedTest') {
            loadSpeedTests();
        } else if (currentTab === 'ssl') {
            loadSslCertificates();
        }
    }, 300000);
}

async function loadUrls() {
    try {
        const response = await fetch(API_BASE);
        const urls = await response.json();

        const urlList = document.getElementById('urlList');
        const emptyState = document.getElementById('emptyState');

        if (urls.length === 0) {
            urlList.innerHTML = '';
            emptyState.style.display = 'block';
            return;
        }

        emptyState.style.display = 'none';
        renderUrls(urls);
    } catch (error) {
        console.error('Error loading URLs:', error);
        showNotification('Error loading URLs', 'error');
    }
}

function renderUrls(urls) {
    const urlList = document.getElementById('urlList');
    urlList.innerHTML = '';

    urls.forEach(url => {
        // Store URL data for filtering
        urlsData[url.id] = url;

        const urlItem = createUrlItem(url);
        urlList.appendChild(urlItem);

        // Create chart for this URL with default 1 day range
        if (url.checks && url.checks.length > 0) {
            setTimeout(() => createChart(url, 1), 0);
        }
    });
}

function createUrlItem(url) {
    const div = document.createElement('div');
    div.className = 'url-item';
    div.id = `url-${url.id}`;

    // Determine if URL is responding slowly
    const isSlow = isRespondingSlowly(url);
    if (isSlow) {
        div.classList.add('slow');
    }

    const latestCheck = url.latestCheck;
    const avgResponseTime = Math.round(url.averageResponseTime);
    const latestResponseTime = latestCheck ? latestCheck.responseTimeMs : 'N/A';
    const statusCode = latestCheck ? latestCheck.statusCode : 'N/A';
    const isSuccess = latestCheck ? latestCheck.isSuccess : false;
    const checkType = url.checkType !== undefined ? url.checkType : 0; // Default to HTTP if undefined
    const checkTypeText = checkType === 0 ? 'HTTP' : 'PING';
    const checkTypeBadge = checkType === 0 ? 'check-badge-http' : 'check-badge-ping';

    div.innerHTML = `
        <div class="url-header">
            <div class="url-info">
                <h3>
                    ${escapeHtml(url.friendlyName)}
                    <span class="check-badge ${checkTypeBadge}">${checkTypeText}</span>
                </h3>
                <a href="${escapeHtml(url.url)}" target="_blank" class="url-link">${escapeHtml(url.url)}</a>
            </div>
            <div class="url-actions">
                <button class="icon-btn" onclick="openModal(${url.id})" title="Edit">
                    <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
                    </svg>
                </button>
                <button class="icon-btn delete" onclick="deleteUrl(${url.id})" title="Delete">
                    <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                    </svg>
                </button>
            </div>
        </div>

        <div class="url-stats">
            <div class="stat">
                <div class="stat-label">Status</div>
                <div class="stat-value ${isSuccess ? 'success' : 'danger'}">${statusCode}</div>
            </div>
            <div class="stat">
                <div class="stat-label">Latest Response</div>
                <div class="stat-value">${latestResponseTime} ms</div>
            </div>
            <div class="stat">
                <div class="stat-label">Average Response</div>
                <div class="stat-value">${avgResponseTime} ms</div>
            </div>
        </div>

        <div class="chart-controls">
            <label class="timerange-label">Time Range:</label>
            <div class="timerange-buttons">
                <button class="timerange-btn active" data-days="1" onclick="changeTimeRange(${url.id}, 1, this)">1d</button>
                <button class="timerange-btn" data-days="3" onclick="changeTimeRange(${url.id}, 3, this)">3d</button>
                <button class="timerange-btn" data-days="5" onclick="changeTimeRange(${url.id}, 5, this)">5d</button>
                <button class="timerange-btn" data-days="14" onclick="changeTimeRange(${url.id}, 14, this)">14d</button>
                <button class="timerange-btn" data-days="30" onclick="changeTimeRange(${url.id}, 30, this)">30d</button>
            </div>
        </div>

        <div class="chart-container">
            <canvas id="chart-${url.id}"></canvas>
        </div>
    `;

    return div;
}

function isRespondingSlowly(url) {
    if (!url.latestCheck || !url.averageResponseTime) {
        return false;
    }

    // Consider it slow if latest response is 50% slower than average
    const threshold = url.averageResponseTime * 1.5;
    return url.latestCheck.responseTimeMs > threshold && url.latestCheck.responseTimeMs > 1000;
}

function convertUtcToLocalTime(utcDateString, includeDatePart = false) {
    // Parse the UTC date string and convert to local time
    // If the string doesn't end with 'Z', add it to indicate UTC
    const dateString = utcDateString.endsWith('Z') ? utcDateString : utcDateString + 'Z';
    const date = new Date(dateString);

    if (includeDatePart) {
        return date.toLocaleString(undefined, {
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    return date.toLocaleTimeString();
}

function createChart(url, days = 1) {
    const ctx = document.getElementById(`chart-${url.id}`);
    if (!ctx) return;

    // Destroy existing chart if it exists
    if (charts[url.id]) {
        charts[url.id].destroy();
    }

    // Filter checks by date range
    const now = new Date();
    const cutoffDate = new Date(now.getTime() - (days * 24 * 60 * 60 * 1000));

    const filteredChecks = url.checks.filter(c => {
        // Ensure UTC date is properly parsed
        const dateString = c.checkedAt.endsWith('Z') ? c.checkedAt : c.checkedAt + 'Z';
        const checkDate = new Date(dateString);
        return checkDate >= cutoffDate;
    });

    console.log(`URL ${url.id}: Showing ${days} days, filtered ${filteredChecks.length} checks from ${url.checks.length} total`);

    // If no data in range, show message
    if (filteredChecks.length === 0) {
        console.log(`No data available for ${days} day(s)`);
        return;
    }

    const checks = [...filteredChecks].reverse(); // Oldest to newest

    // Create labels based on time range
    const labels = checks.map(c => {
        // Ensure UTC date is properly parsed and converted to local time
        const dateString = c.checkedAt.endsWith('Z') ? c.checkedAt : c.checkedAt + 'Z';
        const date = new Date(dateString);

        if (days === 1) {
            // Just show time for 1 day
            return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        } else {
            // Show date and time for longer ranges
            return date.toLocaleString(undefined, {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        }
    });

    const data = checks.map(c => c.responseTimeMs);
    const avgResponseTime = url.averageResponseTime;

    // Determine if currently slow
    const isSlow = isRespondingSlowly(url);
    const lineColor = isSlow ? '#e74c3c' : '#27ae60';
    const fillColor = isSlow ? 'rgba(231, 76, 60, 0.1)' : 'rgba(39, 174, 96, 0.1)';

    charts[url.id] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Response Time (ms)',
                data: data,
                borderColor: lineColor,
                backgroundColor: fillColor,
                borderWidth: 2,
                fill: true,
                tension: 0.4,
                pointRadius: showGraphDots ? 3 : 0,
                pointHoverRadius: 5
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    callbacks: {
                        afterLabel: function(context) {
                            const value = context.parsed.y;
                            if (value > avgResponseTime * 1.5) {
                                return 'Slower than average';
                            }
                            return '';
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function(value) {
                            return value + ' ms';
                        }
                    }
                },
                x: {
                    ticks: {
                        maxTicksLimit: days === 1 ? 12 : (days <= 5 ? 10 : 15),
                        maxRotation: 45,
                        minRotation: 0
                    }
                }
            },
            animation: {
                duration: 750
            }
        }
    });
}

function changeTimeRange(urlId, days, buttonElement) {
    // Update button active state
    const container = buttonElement.closest('.chart-controls');
    container.querySelectorAll('.timerange-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    buttonElement.classList.add('active');

    // Get the URL data and redraw chart with new time range
    const url = urlsData[urlId];
    if (url) {
        createChart(url, days);
    }
}

function openModal(id = null) {
    const modal = document.getElementById('urlModal');
    const modalTitle = document.getElementById('modalTitle');
    const submitText = document.getElementById('submitText');
    const form = document.getElementById('urlForm');

    form.reset();

    if (id) {
        // Edit mode
        modalTitle.textContent = 'Edit URL';
        submitText.textContent = 'Update URL';
        loadUrlForEdit(id);
    } else {
        // Add mode
        modalTitle.textContent = 'Add New URL';
        submitText.textContent = 'Add URL';
        document.getElementById('urlId').value = '';
        document.getElementById('checkType').value = '0'; // Default to HTTP
        handleCheckTypeChange(); // Update UI for default type
    }

    modal.classList.add('active');
}

function closeModal() {
    const modal = document.getElementById('urlModal');
    modal.classList.remove('active');
}

async function loadUrlForEdit(id) {
    try {
        const response = await fetch(`${API_BASE}/${id}`);
        const url = await response.json();

        document.getElementById('urlId').value = url.id;
        document.getElementById('friendlyName').value = url.friendlyName;
        document.getElementById('urlInput').value = url.url;
        document.getElementById('checkType').value = url.checkType !== undefined ? url.checkType : 0;

        // Trigger change event to update UI
        handleCheckTypeChange();
    } catch (error) {
        console.error('Error loading URL:', error);
        showNotification('Error loading URL details', 'error');
    }
}

async function handleSubmit(e) {
    e.preventDefault();

    const id = document.getElementById('urlId').value;
    const friendlyName = document.getElementById('friendlyName').value.trim();
    const url = document.getElementById('urlInput').value.trim();
    const checkType = parseInt(document.getElementById('checkType').value);

    const data = {
        friendlyName,
        url,
        checkType
    };

    try {
        let response;
        if (id) {
            // Update
            response = await fetch(`${API_BASE}/${id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            });
        } else {
            // Create
            response = await fetch(API_BASE, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            });
        }

        if (response.ok) {
            closeModal();
            showNotification(id ? 'URL updated successfully' : 'URL added successfully', 'success');
            await loadUrls();
        } else {
            const errorText = await response.text();
            showNotification(errorText || 'Error saving URL', 'error');
        }
    } catch (error) {
        console.error('Error saving URL:', error);
        showNotification('Error saving URL', 'error');
    }
}

async function deleteUrl(id) {
    if (!confirm('Are you sure you want to delete this URL monitor?')) {
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/${id}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            showNotification('URL deleted successfully', 'success');
            await loadUrls();
        } else {
            showNotification('Error deleting URL', 'error');
        }
    } catch (error) {
        console.error('Error deleting URL:', error);
        showNotification('Error deleting URL', 'error');
    }
}

function showNotification(message, type = 'info') {
    // Create a simple notification
    const notification = document.createElement('div');
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        padding: 1rem 1.5rem;
        background: ${type === 'success' ? '#27ae60' : type === 'error' ? '#e74c3c' : '#3498db'};
        color: white;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        z-index: 10000;
        animation: slideIn 0.3s ease;
    `;
    notification.textContent = message;

    document.body.appendChild(notification);

    setTimeout(() => {
        notification.style.animation = 'slideOut 0.3s ease';
        setTimeout(() => notification.remove(), 300);
    }, 3000);
}

function escapeHtml(unsafe) {
    return unsafe
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

// Tab Switching
function setupTabSwitching() {
    const tabButtons = document.querySelectorAll('.tab-btn');
    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabName = btn.getAttribute('data-tab');
            switchTab(tabName);
        });
    });
}

function switchTab(tabName) {
    currentTab = tabName;

    // Update tab buttons
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.remove('active');
        if (btn.getAttribute('data-tab') === tabName) {
            btn.classList.add('active');
        }
    });

    // Update tab content
    document.querySelectorAll('.tab-content').forEach(content => {
        content.classList.remove('active');
    });

    const activeContent = document.getElementById(`${tabName}Tab`);
    if (activeContent) {
        activeContent.classList.add('active');
    }

    // Load data for the active tab if needed
    if (tabName === 'speedTest' && speedTestData.length === 0) {
        loadSpeedTests();
    } else if (tabName === 'ssl' && sslData.length === 0) {
        loadSslCertificates();
    } else if (tabName === 'firewall') {
        loadFirewalls();
    }
}

// Speed Test Functions
async function loadSpeedTests() {
    try {
        const response = await fetch(SPEEDTEST_API);
        const tests = await response.json();

        speedTestData = tests;

        const speedTestContent = document.getElementById('speedTestContent');
        const speedTestEmpty = document.getElementById('speedTestEmpty');

        if (tests.length === 0) {
            speedTestContent.innerHTML = '';
            speedTestEmpty.style.display = 'block';
            return;
        }

        speedTestEmpty.style.display = 'none';
        renderSpeedTests(tests);
    } catch (error) {
        console.error('Error loading speed tests:', error);
        showNotification('Error loading speed tests', 'error');
    }
}

function renderSpeedTests(tests) {
    const container = document.getElementById('speedTestContent');

    // Get latest test
    const latest = tests[0]; // Tests are ordered by TestedAt descending

    container.innerHTML = `
        <div style="margin-bottom: 1.5rem; display: flex; justify-content: flex-end;">
            <button id="runTestBtn" class="btn btn-primary" onclick="runSpeedTestNow()">
                <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-.856.917-1.398 1.667-.986l11.54 6.348a1.125 1.125 0 010 1.971l-11.54 6.347a1.125 1.125 0 01-1.667-.985V5.653z" />
                </svg>
                Run Test Now
            </button>
        </div>

        <div class="speed-stats">
            <div class="speed-card">
                <h3>Download Speed</h3>
                <div>
                    <span class="speed-value">${latest.downloadMbps}</span>
                    <span class="speed-unit">Mbps</span>
                </div>
            </div>
            <div class="speed-card">
                <h3>Upload Speed</h3>
                <div>
                    <span class="speed-value">${latest.uploadMbps}</span>
                    <span class="speed-unit">Mbps</span>
                </div>
            </div>
            <div class="speed-card">
                <h3>Ping</h3>
                <div>
                    <span class="speed-value">${latest.pingMs}</span>
                    <span class="speed-unit">ms</span>
                </div>
            </div>
            <div class="speed-card">
                <h3>Jitter</h3>
                <div>
                    <span class="speed-value">${latest.jitterMs}</span>
                    <span class="speed-unit">ms</span>
                </div>
            </div>
        </div>

        <div class="speed-chart-container">
            <h3>Download Speed</h3>
            <div class="chart-controls">
                <label class="timerange-label">Time Range:</label>
                <div class="timerange-buttons">
                    <button class="timerange-btn active" onclick="changeSpeedTestRange('download', 1, this)">1d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('download', 3, this)">3d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('download', 7, this)">7d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('download', 14, this)">14d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('download', 30, this)">30d</button>
                </div>
            </div>
            <div class="speed-chart">
                <canvas id="downloadChart"></canvas>
            </div>
        </div>

        <div class="speed-chart-container">
            <h3>Upload Speed</h3>
            <div class="chart-controls">
                <label class="timerange-label">Time Range:</label>
                <div class="timerange-buttons">
                    <button class="timerange-btn active" onclick="changeSpeedTestRange('upload', 1, this)">1d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('upload', 3, this)">3d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('upload', 7, this)">7d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('upload', 14, this)">14d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('upload', 30, this)">30d</button>
                </div>
            </div>
            <div class="speed-chart">
                <canvas id="uploadChart"></canvas>
            </div>
        </div>

        <div class="speed-chart-container">
            <h3>Ping & Jitter</h3>
            <div class="chart-controls">
                <label class="timerange-label">Time Range:</label>
                <div class="timerange-buttons">
                    <button class="timerange-btn active" onclick="changeSpeedTestRange('ping', 1, this)">1d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('ping', 3, this)">3d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('ping', 7, this)">7d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('ping', 14, this)">14d</button>
                    <button class="timerange-btn" onclick="changeSpeedTestRange('ping', 30, this)">30d</button>
                </div>
            </div>
            <div class="speed-chart">
                <canvas id="pingChart"></canvas>
            </div>
        </div>
    `;

    // Create charts with default 1 day range
    setTimeout(() => {
        createSpeedTestChart('download', 1);
        createSpeedTestChart('upload', 1);
        createSpeedTestChart('ping', 1);
    }, 0);
}

function createSpeedTestChart(type, days = 1) {
    const canvasId = type + 'Chart';
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    // Destroy existing chart if it exists
    if (speedTestCharts[canvasId]) {
        speedTestCharts[canvasId].destroy();
    }

    // Filter tests by date range
    const now = new Date();
    const cutoffDate = new Date(now.getTime() - (days * 24 * 60 * 60 * 1000));

    const filteredTests = speedTestData.filter(t => {
        const dateString = t.testedAt.endsWith('Z') ? t.testedAt : t.testedAt + 'Z';
        const testDate = new Date(dateString);
        return testDate >= cutoffDate && t.isSuccess;
    });

    if (filteredTests.length === 0) {
        console.log(`No speed test data available for ${days} day(s)`);
        return;
    }

    const tests = [...filteredTests].reverse(); // Oldest to newest

    // Create labels
    const labels = tests.map(t => {
        const dateString = t.testedAt.endsWith('Z') ? t.testedAt : t.testedAt + 'Z';
        const date = new Date(dateString);

        if (days === 1) {
            return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        } else {
            return date.toLocaleString(undefined, {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        }
    });

    let datasets = [];

    if (type === 'download') {
        datasets = [{
            label: 'Download (Mbps)',
            data: tests.map(t => t.downloadMbps),
            borderColor: '#4a90e2',
            backgroundColor: 'rgba(74, 144, 226, 0.1)',
            borderWidth: 2,
            fill: true,
            tension: 0.4,
            pointRadius: showGraphDots ? 3 : 0,
            pointHoverRadius: 5
        }];
    } else if (type === 'upload') {
        datasets = [{
            label: 'Upload (Mbps)',
            data: tests.map(t => t.uploadMbps),
            borderColor: '#27ae60',
            backgroundColor: 'rgba(39, 174, 96, 0.1)',
            borderWidth: 2,
            fill: true,
            tension: 0.4,
            pointRadius: showGraphDots ? 3 : 0,
            pointHoverRadius: 5
        }];
    } else if (type === 'ping') {
        datasets = [
            {
                label: 'Ping (ms)',
                data: tests.map(t => t.pingMs),
                borderColor: '#f39c12',
                backgroundColor: 'rgba(243, 156, 18, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.4,
                yAxisID: 'y',
                pointRadius: showGraphDots ? 3 : 0,
                pointHoverRadius: 5
            },
            {
                label: 'Jitter (ms)',
                data: tests.map(t => t.jitterMs),
                borderColor: '#e74c3c',
                backgroundColor: 'rgba(231, 76, 60, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.4,
                yAxisID: 'y',
                pointRadius: showGraphDots ? 3 : 0,
                pointHoverRadius: 5
            }
        ];
    }

    speedTestCharts[canvasId] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: type === 'ping'
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function(value) {
                            return value + (type === 'ping' ? ' ms' : ' Mbps');
                        }
                    }
                },
                x: {
                    ticks: {
                        maxTicksLimit: days === 1 ? 12 : (days <= 7 ? 10 : 15),
                        maxRotation: 45,
                        minRotation: 0
                    }
                }
            },
            animation: {
                duration: 750
            }
        }
    });
}

function changeSpeedTestRange(type, days, buttonElement) {
    // Update button active state
    const container = buttonElement.closest('.chart-controls');
    container.querySelectorAll('.timerange-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    buttonElement.classList.add('active');

    // Redraw chart with new time range
    createSpeedTestChart(type, days);
}

function toggleGraphDots() {
    // Toggle the global dots setting
    showGraphDots = !showGraphDots;

    // Update button visual state
    const toggleBtn = document.getElementById('toggleDotsBtn');
    if (showGraphDots) {
        toggleBtn.classList.add('active');
    } else {
        toggleBtn.classList.remove('active');
    }

    // Redraw all URL monitoring charts with current time ranges
    Object.keys(urlsData).forEach(urlId => {
        const url = urlsData[urlId];
        if (url && url.checks && url.checks.length > 0) {
            // Get current active time range button for this URL
            const urlElement = document.getElementById(`url-${urlId}`);
            if (urlElement) {
                const activeBtn = urlElement.querySelector('.timerange-btn.active');
                const days = activeBtn ? parseInt(activeBtn.getAttribute('data-days')) : 1;
                createChart(url, days);
            }
        }
    });

    // Redraw all speed test charts with current time ranges
    if (speedTestData.length > 0) {
        // Find active time range buttons for each chart type
        const chartTypes = ['download', 'upload', 'ping'];
        chartTypes.forEach(type => {
            const chartContainer = document.getElementById(type + 'Chart')?.closest('.speed-chart-container');
            if (chartContainer) {
                const activeBtn = chartContainer.querySelector('.timerange-btn.active');
                const days = activeBtn ? parseInt(activeBtn.textContent.replace('d', '')) : 1;
                createSpeedTestChart(type, days);
            }
        });
    }
}

async function runSpeedTestNow() {
    // Find whichever button is visible
    const btn = document.getElementById('runTestBtn') || document.getElementById('runTestBtnEmpty');
    if (!btn) return;

    // Disable button and show loading state
    btn.disabled = true;
    const originalText = btn.innerHTML;
    btn.innerHTML = `
        <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" style="animation: spin 1s linear infinite;">
            <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
        </svg>
        Running Test...
    `;

    try {
        const response = await fetch(`${SPEEDTEST_API}/run`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            showNotification('Speed test completed successfully', 'success');
            // Reload speed test data
            await loadSpeedTests();
        } else {
            // Try to parse as JSON, but handle if it's not valid JSON
            const responseText = await response.text();
            console.error('Speed test failed. Status:', response.status);
            console.error('Response body:', responseText);

            let errorMsg = 'Unknown error';
            let pathInfo = '';

            try {
                const error = JSON.parse(responseText);
                errorMsg = error.error || 'Unknown error';
                pathInfo = error.path ? ` (Path: ${error.path})` : '';
            } catch (e) {
                // Response is not JSON, use the text directly
                errorMsg = responseText.substring(0, 200); // First 200 chars
            }

            showNotification(`Speed test failed: ${errorMsg}${pathInfo}`, 'error');
        }
    } catch (error) {
        console.error('Error running speed test:', error);
        showNotification(`Error running speed test: ${error.message}`, 'error');
    } finally {
        // Re-enable button
        btn.disabled = false;
        btn.innerHTML = originalText;
    }
}

// Add animation styles
const style = document.createElement('style');
style.textContent = `
    @keyframes slideIn {
        from {
            transform: translateX(400px);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
    @keyframes slideOut {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(400px);
            opacity: 0;
        }
    }
    @keyframes spin {
        from {
            transform: rotate(0deg);
        }
        to {
            transform: rotate(360deg);
        }
    }
`;
document.head.appendChild(style);

// SSL Certificate Functions
async function loadSslCertificates() {
    try {
        const response = await fetch(SSL_API);
        const certs = await response.json();

        sslData = certs;

        const sslList = document.getElementById('sslList');
        const sslEmpty = document.getElementById('sslEmpty');

        if (certs.length === 0) {
            sslList.innerHTML = '';
            sslEmpty.style.display = 'block';
            return;
        }

        sslEmpty.style.display = 'none';
        renderSslCertificates(certs);
    } catch (error) {
        console.error('Error loading SSL certificates:', error);
        showNotification('Error loading SSL certificates', 'error');
    }
}

function renderSslCertificates(certs) {
    const container = document.getElementById('sslList');
    container.innerHTML = '';

    certs.forEach(cert => {
        const card = createSslCard(cert);
        container.appendChild(card);
    });
}

function createSslCard(cert) {
    const div = document.createElement('div');

    // Determine status class
    let statusClass = 'valid';
    let statusText = 'Valid';
    let cardClass = '';

    if (cert.daysUntilExpiration < 0) {
        statusClass = 'expired';
        statusText = 'Expired';
        cardClass = 'expired';
    } else if (cert.daysUntilExpiration <= 30) {
        statusClass = 'expiring';
        statusText = 'Expiring Soon';
        cardClass = 'expiring-soon';
    }

    div.className = `ssl-card ${cardClass}`;

    const validFrom = new Date(cert.validFrom).toLocaleDateString();
    const validTo = new Date(cert.validTo).toLocaleDateString();
    const lastChecked = new Date(cert.lastChecked).toLocaleString();

    const daysDisplay = cert.daysUntilExpiration < 0
        ? `<div class="days-number">EXPIRED</div>`
        : `<div class="days-number">${cert.daysUntilExpiration}</div><div class="days-label">days left</div>`;

    div.innerHTML = `
        <div class="days-badge ${statusClass}">
            ${daysDisplay}
        </div>

        <div class="ssl-header">
            <div class="ssl-title">${escapeHtml(cert.friendlyName)}</div>
            <div class="ssl-url">${escapeHtml(cert.url)}</div>
            <div class="ssl-info">
                <div class="ssl-info-item">
                    <span class="ssl-label">Status</span>
                    <span class="ssl-status ${statusClass}">${statusText}</span>
                </div>
                <div class="ssl-info-item">
                    <span class="ssl-label">Valid From</span>
                    <span class="ssl-value">${validFrom}</span>
                </div>
                <div class="ssl-info-item">
                    <span class="ssl-label">Valid To</span>
                    <span class="ssl-value">${validTo}</span>
                </div>
                <div class="ssl-info-item">
                    <span class="ssl-label">Issuer</span>
                    <span class="ssl-value">${escapeHtml(cert.issuer.split(',')[0])}</span>
                </div>
                <div class="ssl-info-item">
                    <span class="ssl-label">Last Checked</span>
                    <span class="ssl-value">${lastChecked}</span>
                </div>
            </div>
        </div>

        <div class="ssl-actions">
            <button class="icon-btn" onclick="checkSslNow(${cert.id})" title="Check Now">
                <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
                </svg>
            </button>
            <button class="icon-btn" onclick="openSslModal(${cert.id})" title="Edit">
                <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
                </svg>
            </button>
            <button class="icon-btn delete" onclick="deleteSslCertificate(${cert.id})" title="Delete">
                <svg class="icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                </svg>
            </button>
        </div>
    `;

    return div;
}

function openSslModal(id = null) {
    const modal = document.getElementById('sslModal');
    const modalTitle = document.getElementById('sslModalTitle');
    const submitText = document.getElementById('sslSubmitText');
    const form = document.getElementById('sslForm');

    form.reset();

    if (id) {
        // Edit mode
        modalTitle.textContent = 'Edit SSL Certificate';
        submitText.textContent = 'Update Certificate';
        loadSslForEdit(id);
    } else {
        // Add mode
        modalTitle.textContent = 'Add SSL Certificate';
        submitText.textContent = 'Add Certificate';
        document.getElementById('sslId').value = '';
    }

    modal.classList.add('active');
}

function closeSslModal() {
    const modal = document.getElementById('sslModal');
    modal.classList.remove('active');
}

async function loadSslForEdit(id) {
    try {
        const response = await fetch(`${SSL_API}/${id}`);
        const cert = await response.json();

        document.getElementById('sslId').value = cert.id;
        document.getElementById('sslFriendlyName').value = cert.friendlyName;
        document.getElementById('sslUrl').value = cert.url;
    } catch (error) {
        console.error('Error loading SSL certificate:', error);
        showNotification('Error loading certificate details', 'error');
    }
}

async function handleSslSubmit(e) {
    e.preventDefault();

    const id = document.getElementById('sslId').value;
    const friendlyName = document.getElementById('sslFriendlyName').value.trim();
    const url = document.getElementById('sslUrl').value.trim();

    const data = {
        friendlyName,
        url
    };

    try {
        let response;
        if (id) {
            // Update
            data.id = parseInt(id);
            response = await fetch(`${SSL_API}/${id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            });
        } else {
            // Create
            response = await fetch(SSL_API, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            });
        }

        if (response.ok) {
            closeSslModal();
            showNotification(id ? 'Certificate updated successfully' : 'Certificate added successfully', 'success');
            await loadSslCertificates();
        } else {
            const errorText = await response.text();
            showNotification(errorText || 'Error saving certificate', 'error');
        }
    } catch (error) {
        console.error('Error saving SSL certificate:', error);
        showNotification('Error saving certificate', 'error');
    }
}

async function deleteSslCertificate(id) {
    if (!confirm('Are you sure you want to delete this SSL certificate monitor?')) {
        return;
    }

    try {
        const response = await fetch(`${SSL_API}/${id}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            showNotification('Certificate deleted successfully', 'success');
            await loadSslCertificates();
        } else {
            showNotification('Error deleting certificate', 'error');
        }
    } catch (error) {
        console.error('Error deleting SSL certificate:', error);
        showNotification('Error deleting certificate', 'error');
    }
}

async function checkSslNow(id) {
    try {
        showNotification('Checking SSL certificate...', 'info');
        const response = await fetch(`${SSL_API}/${id}/check`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Certificate checked successfully', 'success');
            await loadSslCertificates();
        } else {
            showNotification('Error checking certificate', 'error');
        }
    } catch (error) {
        console.error('Error checking SSL certificate:', error);
        showNotification('Error checking certificate', 'error');
    }
}

// Dark Mode Functions
function toggleDarkMode() {
    document.body.classList.toggle('dark-mode');
    const isDarkMode = document.body.classList.contains('dark-mode');
    localStorage.setItem('darkMode', isDarkMode ? 'enabled' : 'disabled');

    // Update button visual state
    const darkModeBtn = document.getElementById('darkModeBtn');
    if (isDarkMode) {
        darkModeBtn.classList.add('active');
    } else {
        darkModeBtn.classList.remove('active');
    }
}

function loadDarkModePreference() {
    const darkMode = localStorage.getItem('darkMode');
    if (darkMode === 'enabled') {
        document.body.classList.add('dark-mode');
        document.getElementById('darkModeBtn').classList.add('active');
    }
}

// Close modal when clicking outside
document.getElementById('urlModal').addEventListener('click', (e) => {
    if (e.target.id === 'urlModal') {
        closeModal();
    }
});

document.getElementById('sslModal').addEventListener('click', (e) => {
    if (e.target.id === 'sslModal') {
        closeSslModal();
    }
});

// Logout function
async function logout() {
    try {
        await fetch('/api/auth/logout', { method: 'POST' });
        window.location.href = '/login.html';
    } catch (error) {
        console.error('Logout error:', error);
        window.location.href = '/login.html';
    }
}

// ============================================
// FIREWALL TAB FUNCTIONALITY
// ============================================

let selectedFirewall = null;

// Load firewalls on page load
async function loadFirewalls() {
    try {
        const response = await fetch('/api/firewall');
        if (!response.ok) throw new Error('Failed to load firewalls');

        const firewalls = await response.json();
        const select = document.getElementById('firewallSelect');

        // Clear existing options except first
        select.innerHTML = '<option value="">Select a firewall...</option>';

        firewalls.forEach(fw => {
            const option = document.createElement('option');
            option.value = fw.id;
            option.textContent = fw.friendlyName;
            select.appendChild(option);
        });

        // If we had a selected firewall, try to reselect it
        if (selectedFirewall) {
            select.value = selectedFirewall.id;
            showFirewallInfo(selectedFirewall);
        }
    } catch (error) {
        console.error('Error loading firewalls:', error);
    }
}

// Firewall selection changed
document.getElementById('firewallSelect').addEventListener('change', async (e) => {
    const firewallId = e.target.value;
    if (!firewallId) {
        selectedFirewall = null;
        document.getElementById('firewallInfo').style.display = 'none';
        return;
    }

    try {
        const response = await fetch(`/api/firewall/${firewallId}`);
        if (!response.ok) throw new Error('Failed to load firewall');

        selectedFirewall = await response.json();
        showFirewallInfo(selectedFirewall);
    } catch (error) {
        console.error('Error loading firewall:', error);
        alert('Error loading firewall details');
    }
});

function showFirewallInfo(firewall) {
    document.getElementById('fwHostname').textContent = firewall.firewallHostname || 'N/A';
    document.getElementById('fwModel').textContent = firewall.model || 'N/A';
    document.getElementById('fwSerial').textContent = firewall.serialNumber || 'N/A';
    document.getElementById('fwVersion').textContent = firewall.softwareVersion || 'N/A';
    document.getElementById('firewallInfo').style.display = 'block';
}

// Add Firewall button
document.getElementById('addFirewallBtn').addEventListener('click', () => {
    document.getElementById('firewallModalTitle').textContent = 'Add Firewall';
    document.getElementById('firewallSubmitText').textContent = 'Add Firewall';
    document.getElementById('firewallId').value = '';
    document.getElementById('firewallForm').reset();
    document.getElementById('firewallFormError').style.display = 'none';
    document.getElementById('firewallModal').classList.add('show');
});

// Edit Firewall button
document.getElementById('editFirewallBtn').addEventListener('click', () => {
    if (!selectedFirewall) return;

    document.getElementById('firewallModalTitle').textContent = 'Edit Firewall';
    document.getElementById('firewallSubmitText').textContent = 'Update Firewall';
    document.getElementById('firewallId').value = selectedFirewall.id;
    document.getElementById('firewallFriendlyName').value = selectedFirewall.friendlyName;
    document.getElementById('firewallHostname').value = selectedFirewall.hostname;
    document.getElementById('firewallUsername').value = '';
    document.getElementById('firewallPassword').value = '';
    document.getElementById('firewallFormError').style.display = 'none';
    document.getElementById('firewallModal').classList.add('show');
});

// Delete Firewall button
document.getElementById('deleteFirewallBtn').addEventListener('click', async () => {
    if (!selectedFirewall) return;

    if (!confirm(`Are you sure you want to delete "${selectedFirewall.friendlyName}"?`)) {
        return;
    }

    try {
        const response = await fetch(`/api/firewall/${selectedFirewall.id}`, {
            method: 'DELETE'
        });

        if (!response.ok) throw new Error('Failed to delete firewall');

        selectedFirewall = null;
        await loadFirewalls();
        document.getElementById('firewallInfo').style.display = 'none';
    } catch (error) {
        console.error('Error deleting firewall:', error);
        alert('Error deleting firewall');
    }
});

// Firewall form submit
document.getElementById('firewallForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const firewallId = document.getElementById('firewallId').value;
    const isEdit = !!firewallId;

    const data = {
        friendlyName: document.getElementById('firewallFriendlyName').value,
        hostname: document.getElementById('firewallHostname').value,
        username: document.getElementById('firewallUsername').value,
        password: document.getElementById('firewallPassword').value
    };

    const errorDiv = document.getElementById('firewallFormError');
    errorDiv.style.display = 'none';

    try {
        const url = isEdit ? `/api/firewall/${firewallId}` : '/api/firewall';
        const method = isEdit ? 'PUT' : 'POST';

        const response = await fetch(url, {
            method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || 'Failed to save firewall');
        }

        if (!isEdit) {
            const newFirewall = await response.json();
            selectedFirewall = newFirewall;
        }

        await loadFirewalls();
        closeFirewallModal();

        if (selectedFirewall) {
            document.getElementById('firewallSelect').value = selectedFirewall.id;
            showFirewallInfo(selectedFirewall);
        }
    } catch (error) {
        console.error('Error saving firewall:', error);
        errorDiv.textContent = error.message;
        errorDiv.style.display = 'block';
    }
});

function closeFirewallModal() {
    document.getElementById('firewallModal').classList.remove('show');
}

// Close modal when clicking outside
document.getElementById('firewallModal').addEventListener('click', (e) => {
    if (e.target.id === 'firewallModal') {
        closeFirewallModal();
    }
});

// Test URL form
document.getElementById('testUrlForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    if (!selectedFirewall) {
        alert('Please select a firewall first');
        return;
    }

    const sourceIp = document.getElementById('testSourceIp').value;
    const url = document.getElementById('testUrl').value;

    const resultsDiv = document.getElementById('testUrlResults');
    resultsDiv.innerHTML = '<div class="loading">Testing URL...</div>';
    resultsDiv.style.display = 'block';

    try {
        const response = await fetch(`/api/firewall/${selectedFirewall.id}/test-url`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sourceIp, url })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || 'Test failed');
        }

        const result = await response.json();

        resultsDiv.innerHTML = `
            <div class="test-result">
                <h3>Test Result</h3>
                <div class="result-grid">
                    <div class="result-item">
                        <span class="result-label">URL:</span>
                        <span class="result-value">${escapeHtml(result.url)}</span>
                    </div>
                    <div class="result-item">
                        <span class="result-label">Source IP:</span>
                        <span class="result-value">${escapeHtml(result.sourceIp)}</span>
                    </div>
                    <div class="result-item">
                        <span class="result-label">Category:</span>
                        <span class="result-value result-category">${escapeHtml(result.category)}</span>
                    </div>
                </div>
            </div>
        `;
    } catch (error) {
        console.error('Error testing URL:', error);
        resultsDiv.innerHTML = `<div class="error-message show">${escapeHtml(error.message)}</div>`;
    }
});

// Query Logs form
document.getElementById('queryLogsForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    if (!selectedFirewall) {
        alert('Please select a firewall first');
        return;
    }

    const sourceIp = document.getElementById('querySourceIp').value;
    const domain = document.getElementById('queryDomain').value;
    const hoursAgo = parseInt(document.getElementById('queryTimeWindow').value);

    const resultsDiv = document.getElementById('queryLogsResults');
    const dnsInfoDiv = document.getElementById('dnsResolutionInfo');
    const tableDiv = document.getElementById('logsTableContainer');

    resultsDiv.style.display = 'block';
    dnsInfoDiv.style.display = 'none';
    tableDiv.innerHTML = '<div class="loading">Querying logs...</div>';

    try {
        const response = await fetch(`/api/firewall/${selectedFirewall.id}/query-logs`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sourceIp, domain, hoursAgo })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || 'Query failed');
        }

        const result = await response.json();

        // Show DNS resolution info if domain was provided
        if (domain && result.dnsResolution) {
            const dns = result.dnsResolution;
            let dnsHtml = `<div class="dns-resolution"><strong>DNS Resolution for ${escapeHtml(domain)}:</strong> `;

            if (dns.isSinkhole) {
                dnsHtml += `<span class="sinkhole-warning">⚠️ Sinkholed to ${escapeHtml(dns.resolvedName)}</span>`;
            } else if (dns.resolvedIps && dns.resolvedIps.length > 0) {
                dnsHtml += `<span class="resolved-ips">${dns.resolvedIps.map(ip => escapeHtml(ip)).join(', ')}</span>`;
            } else {
                dnsHtml += `<span class="dns-error">Unable to resolve</span>`;
            }

            dnsHtml += '</div>';
            dnsInfoDiv.innerHTML = dnsHtml;
            dnsInfoDiv.style.display = 'block';
        }

        // Show logs table
        if (result.logs && result.logs.length > 0) {
            let tableHtml = `
                <div class="logs-table-wrapper">
                    <table class="logs-table">
                        <thead>
                            <tr>
                                <th>Time</th>
                                <th>Source IP</th>
                                <th>Dest IP</th>
                                <th>Dest Port</th>
                                <th>App</th>
                                <th>Action</th>
                                <th>End Reason</th>
                                <th>Bytes</th>
                                <th>Sent</th>
                                <th>Received</th>
                                <th>Category</th>
                            </tr>
                        </thead>
                        <tbody>
            `;

            result.logs.forEach(log => {
                const actionClass = log.action === 'allow' ? 'action-allow' : 'action-deny';
                tableHtml += `
                    <tr>
                        <td>${escapeHtml(log.timeGenerated)}</td>
                        <td>${escapeHtml(log.sourceIp)}</td>
                        <td>${escapeHtml(log.destinationIp)}</td>
                        <td>${escapeHtml(log.destinationPort)}</td>
                        <td>${escapeHtml(log.application)}</td>
                        <td class="${actionClass}">${escapeHtml(log.action)}</td>
                        <td>${escapeHtml(log.sessionEndReason)}</td>
                        <td>${formatBytes(log.bytes)}</td>
                        <td>${formatBytes(log.bytesSent)}</td>
                        <td>${formatBytes(log.bytesReceived)}</td>
                        <td>${escapeHtml(log.category || 'N/A')}</td>
                    </tr>
                `;
            });

            tableHtml += `
                        </tbody>
                    </table>
                </div>
                <div class="logs-summary">Found ${result.logs.length} log entries</div>
            `;

            tableDiv.innerHTML = tableHtml;
        } else {
            tableDiv.innerHTML = '<div class="empty-state"><p>No log entries found</p></div>';
        }
    } catch (error) {
        console.error('Error querying logs:', error);
        tableDiv.innerHTML = `<div class="error-message show">${escapeHtml(error.message)}</div>`;
    }
});

function formatBytes(bytes) {
    const num = parseInt(bytes);
    if (isNaN(num) || num === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(num) / Math.log(k));
    return Math.round(num / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}
