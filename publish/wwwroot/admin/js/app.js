/* ============================================================
   PRYXEL Admin Panel – Application Logic
   ============================================================ */

const API = '/api';
let token = localStorage.getItem('adminToken') || '';
let adminName = localStorage.getItem('adminName') || 'Admin';

// Data stores
let allUsers = [];
let allProjects = [];
let allNotifications = [];

// ===================== INIT =====================
document.addEventListener('DOMContentLoaded', () => {
    if (token) {
        showApp();
    }
});

// ===================== AUTH =====================
async function handleLogin() {
    const email = document.getElementById('loginEmail').value.trim();
    const password = document.getElementById('loginPassword').value.trim();
    const errorEl = document.getElementById('loginError');
    errorEl.textContent = '';

    if (!email || !password) {
        errorEl.textContent = 'Please enter email and password';
        return;
    }

    try {
        const res = await fetch(`${API}/user/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });
        const data = await res.json();

        if (!res.ok) {
            errorEl.textContent = data.message || data.error || 'Login failed';
            return;
        }

        if (data.role !== 'Admin') {
            errorEl.textContent = 'Only admin accounts can access this panel';
            return;
        }

        token = data.token;
        adminName = `${data.firstName || ''} ${data.lastName || ''}`.trim() || 'Admin';
        localStorage.setItem('adminToken', token);
        localStorage.setItem('adminName', adminName);
        showApp();
    } catch (e) {
        errorEl.textContent = 'Connection error. Is the server running?';
    }
}

function handleLogout() {
    if (!confirm('Are you sure you want to logout?')) return;
    token = '';
    localStorage.removeItem('adminToken');
    localStorage.removeItem('adminName');
    document.getElementById('appPage').style.display = 'none';
    document.getElementById('loginPage').style.display = 'flex';
    document.getElementById('loginEmail').value = '';
    document.getElementById('loginPassword').value = '';
}

function showApp() {
    document.getElementById('loginPage').style.display = 'none';
    document.getElementById('appPage').style.display = 'flex';
    document.getElementById('adminNameDisplay').textContent = adminName;
    // Explicitly show hamburger button on mobile after login
    const hamburger = document.getElementById('hamburgerBtn');
    if (window.innerWidth <= 1024) {
        hamburger.style.display = 'flex';
    } else {
        hamburger.style.display = 'none';
    }
    switchPage('dashboard');
}

// ===================== SIDEBAR (MOBILE) =====================
function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebarOverlay');
    const hamburger = document.getElementById('hamburgerBtn');
    const isOpen = sidebar.classList.contains('open');
    if (isOpen) {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
        hamburger.style.display = 'flex'; // show hamburger again
    } else {
        sidebar.classList.add('open');
        overlay.classList.add('active');
        hamburger.style.display = 'none'; // hide hamburger — use ✕ inside sidebar
    }
}

function closeSidebar() {
    document.getElementById('sidebar').classList.remove('open');
    document.getElementById('sidebarOverlay').classList.remove('active');
    // Only restore hamburger if on mobile
    if (window.innerWidth <= 1024) {
        document.getElementById('hamburgerBtn').style.display = 'flex';
    }
}

// ===================== NAVIGATION =====================
function switchPage(page) {
    document.querySelectorAll('.page-section').forEach(el => el.style.display = 'none');
    document.querySelectorAll('.sidebar-item').forEach(el => el.classList.remove('active'));

    document.getElementById(`page-${page}`).style.display = 'block';
    document.querySelector(`.sidebar-item[data-page="${page}"]`).classList.add('active');

    // Auto-close sidebar on mobile when a page is selected
    if (window.innerWidth <= 1024) {
        closeSidebar();
    }

    // Load data for the page
    switch (page) {
        case 'dashboard': loadDashboard(); break;
        case 'users': loadUsers(); break;
        case 'projects': loadProjects(); break;
        case 'notifications': loadNotifications(); break;
        case 'trending': loadTrending(); break;
    }
}

// ===================== HELPERS =====================
function api(endpoint, options = {}) {
    const headers = { ...options.headers };
    if (options.body) headers['Content-Type'] = 'application/json';
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return fetch(`${API}${endpoint}`, { ...options, headers });
}

function toast(message, type = 'success') {
    const container = document.getElementById('toastContainer');
    const el = document.createElement('div');
    el.className = `toast ${type}`;
    el.textContent = message;
    container.appendChild(el);
    setTimeout(() => el.remove(), 3500);
}

function formatDate(dateStr) {
    if (!dateStr) return '-';
    try {
        const d = new Date(dateStr);
        return `${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    } catch { return dateStr; }
}

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function closeModal() {
    document.getElementById('modalContainer').innerHTML = '';
}

// ===================== DASHBOARD =====================
async function loadDashboard() {
    const grid = document.getElementById('statsGrid');
    grid.innerHTML = '<div class="spinner"></div>';

    try {
        const res = await api('/admin/stats');
        const stats = await res.json();

        const cards = [
            { icon: '👥', label: 'Total Users', value: stats.totalUsers, color: 'blue' },
            { icon: '🎓', label: 'Students', value: stats.totalStudents, color: 'green' },
            { icon: '👨‍🏫', label: 'Teachers', value: stats.totalTeachers, color: 'orange' },
            { icon: '📁', label: 'Total Projects', value: stats.totalProjects, color: 'purple' },
            { icon: '✅', label: 'Completed', value: stats.completedProjects, color: 'green' },
            { icon: '🔄', label: 'Ongoing', value: stats.ongoingProjects, color: 'orange' },
            { icon: '📤', label: 'Uploaded', value: stats.uploadedProjects, color: 'teal' },
            { icon: '📄', label: 'Total Files', value: stats.totalFiles, color: 'blue' },
            { icon: '🔔', label: 'Notifications', value: stats.totalNotifications, color: 'red' },
            { icon: '🆕', label: 'Recent Users (7d)', value: stats.recentUsers, color: 'purple' },
        ];

        grid.innerHTML = cards.map(c => `
      <div class="stat-card">
        <div class="stat-icon ${c.color}">${c.icon}</div>
        <div class="stat-info">
          <h3>${c.value ?? 0}</h3>
          <p>${c.label}</p>
        </div>
      </div>
    `).join('');

        // Completion rate
        const total = (stats.completedProjects || 0) + (stats.ongoingProjects || 0);
        const pct = total > 0 ? Math.round((stats.completedProjects / total) * 100) : 0;
        document.getElementById('completionPct').textContent = `${pct}%`;
        document.getElementById('completionBar').style.width = `${pct}%`;
    } catch (e) {
        grid.innerHTML = '<div class="empty-state"><p>Failed to load stats</p></div>';
    }
}

// ===================== USERS =====================
async function loadUsers() {
    const container = document.getElementById('usersTable');
    container.innerHTML = '<div class="spinner"></div>';

    try {
        const res = await api('/admin/users');
        const data = await res.json();
        allUsers = data.users || [];
        renderUsers(allUsers);
    } catch (e) {
        container.innerHTML = '<div class="empty-state"><p>Failed to load users</p></div>';
    }
}

function filterUsers() {
    const search = document.getElementById('userSearch').value.toLowerCase();
    const role = document.getElementById('userRoleFilter').value;

    let filtered = allUsers;
    if (search) {
        filtered = filtered.filter(u =>
            `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(search)
        );
    }
    if (role) {
        filtered = filtered.filter(u => u.role === role);
    }
    renderUsers(filtered);
}

function renderUsers(users) {
    const container = document.getElementById('usersTable');
    if (!users.length) {
        container.innerHTML = '<div class="empty-state"><div class="icon">👥</div><p>No users found</p></div>';
        return;
    }

    container.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Email</th>
          <th>Role</th>
          <th>Register No</th>
          <th>Status</th>
          <th>Joined</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        ${users.map(u => `
          <tr>
            <td><strong>${escapeHtml(u.firstName || '')} ${escapeHtml(u.lastName || '')}</strong></td>
            <td>${escapeHtml(u.email)}</td>
            <td><span class="badge badge-${(u.role || '').toLowerCase()}">${u.role}</span></td>
            <td>${escapeHtml(u.registerNumber || '-')}</td>
            <td><span class="badge ${u.isApproved ? 'badge-approved' : 'badge-blocked'}">${u.isApproved ? 'Approved' : 'Blocked'}</span></td>
            <td>${formatDate(u.createdAt)}</td>
            <td style="display:flex;gap:4px;">
              <button class="btn-icon edit" title="Change Role" onclick="changeUserRole(${u.id}, '${u.role}')">✏️</button>
              <button class="btn-icon toggle" title="${u.isApproved ? 'Block' : 'Approve'}" onclick="toggleUserApproval(${u.id}, ${u.isApproved})">${u.isApproved ? '🚫' : '✅'}</button>
              <button class="btn-icon danger" title="Delete" onclick="deleteUser(${u.id}, '${escapeHtml(u.firstName)} ${escapeHtml(u.lastName)}')">🗑️</button>
            </td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;
}

async function changeUserRole(userId, currentRole) {
    const roles = ['Student', 'Teacher', 'Admin'].filter(r => r !== currentRole);
    const newRole = prompt(`Change role from "${currentRole}" to:\n\n${roles.map((r, i) => `${i + 1}. ${r}`).join('\n')}\n\nEnter role name:`);
    if (!newRole || !['Student', 'Teacher', 'Admin'].includes(newRole)) return;

    try {
        const res = await api(`/admin/users/${userId}/role`, {
            method: 'PUT',
            body: JSON.stringify({ role: newRole })
        });
        if (res.ok) {
            toast('Role updated successfully');
            loadUsers();
        } else {
            toast('Failed to update role', 'error');
        }
    } catch { toast('Error updating role', 'error'); }
}

async function toggleUserApproval(userId, isApproved) {
    const action = isApproved ? 'block' : 'approve';
    if (!confirm(`Are you sure you want to ${action} this user?`)) return;

    try {
        const res = await api(`/admin/users/${userId}/approve`, { method: 'PUT' });
        if (res.ok) {
            toast(`User ${action}d successfully`);
            loadUsers();
        } else {
            toast(`Failed to ${action} user`, 'error');
        }
    } catch { toast('Error updating user', 'error'); }
}

async function deleteUser(userId, name) {
    if (!confirm(`Delete user "${name}"? This action cannot be undone.`)) return;

    try {
        const res = await api(`/admin/users/${userId}`, { method: 'DELETE' });
        if (res.ok) {
            toast('User deleted');
            loadUsers();
        } else {
            toast('Failed to delete user', 'error');
        }
    } catch { toast('Error deleting user', 'error'); }
}

function showAddUserModal() {
    document.getElementById('modalContainer').innerHTML = `
    <div class="modal-backdrop" onclick="if(event.target===this)closeModal()">
      <div class="modal">
        <div class="modal-header">
          <h3>Add New User</h3>
          <button class="modal-close" onclick="closeModal()">&times;</button>
        </div>
        <div class="modal-body">
          <div class="form-group"><label>First Name</label><input type="text" id="newFirstName" placeholder="First name"></div>
          <div class="form-group"><label>Last Name</label><input type="text" id="newLastName" placeholder="Last name"></div>
          <div class="form-group"><label>Email</label><input type="email" id="newEmail" placeholder="email@example.com"></div>
          <div class="form-group"><label>Password</label><input type="password" id="newPassword" placeholder="Password"></div>
          <div class="form-group"><label>Role</label>
            <select id="newRole">
              <option value="Student">Student</option>
              <option value="Teacher">Teacher</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
          <div class="form-group"><label>Register Number (optional)</label><input type="text" id="newRegNumber" placeholder="e.g., 22BCA001" style="text-transform:uppercase"></div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" onclick="closeModal()">Cancel</button>
          <button class="btn btn-primary" onclick="createUser()">Create User</button>
        </div>
      </div>
    </div>
  `;
}

async function createUser() {
    const body = {
        firstName: document.getElementById('newFirstName').value.trim(),
        lastName: document.getElementById('newLastName').value.trim(),
        email: document.getElementById('newEmail').value.trim(),
        password: document.getElementById('newPassword').value.trim(),
        role: document.getElementById('newRole').value,
        registerNumber: document.getElementById('newRegNumber').value.trim().toUpperCase()
    };

    if (!body.firstName || !body.email || !body.password) {
        toast('Please fill in required fields', 'error');
        return;
    }

    try {
        const res = await api('/admin/users', {
            method: 'POST',
            body: JSON.stringify(body)
        });
        if (res.ok) {
            closeModal();
            toast('User created successfully');
            loadUsers();
        } else {
            const err = await res.json();
            toast(err.error || 'Failed to create user', 'error');
        }
    } catch { toast('Error creating user', 'error'); }
}

// ===================== PROJECTS =====================
async function loadProjects() {
    const container = document.getElementById('projectsGrid');
    container.innerHTML = '<div class="spinner"></div>';

    try {
        const res = await api('/admin/projects');
        const data = await res.json();
        allProjects = data.projects || [];
        renderProjects(allProjects);
    } catch (e) {
        container.innerHTML = '<div class="empty-state"><p>Failed to load projects</p></div>';
    }
}

function filterProjects() {
    const search = document.getElementById('projectSearch').value.toLowerCase();
    const status = document.getElementById('projectStatusFilter').value;

    let filtered = allProjects;
    if (search) {
        filtered = filtered.filter(p =>
            `${p.title} ${p.batch} ${p.createdBy} ${p.teamMembers}`.toLowerCase().includes(search)
        );
    }
    if (status) {
        filtered = filtered.filter(p => p.status === status);
    }
    renderProjects(filtered);
}

function renderProjects(projects) {
    const container = document.getElementById('projectsGrid');
    if (!projects.length) {
        container.innerHTML = '<div class="empty-state"><div class="icon">📁</div><p>No projects found</p></div>';
        return;
    }

    container.innerHTML = projects.map(p => `
    <div class="project-card">
      <div style="display:flex;justify-content:space-between;align-items:start;">
        <h4>${escapeHtml(p.title)}</h4>
        <span class="badge badge-${(p.status || 'ongoing').toLowerCase()}">${p.status}</span>
      </div>
      <div class="meta-row">👤 ${escapeHtml(p.createdBy || '-')}</div>
      <div class="meta-row">👥 ${escapeHtml(p.teamMembers || '-')}</div>
      <div class="meta-row">📅 Batch: ${escapeHtml(p.batch || '-')}</div>
      <div class="meta-row">📄 Files: ${p.fileCount || 0}</div>
      <div class="desc">${escapeHtml(p.description || p.abstraction || 'No description')}</div>
      <div class="actions">
        <button class="btn btn-sm btn-primary" onclick="viewProject(${p.projectId})">View</button>
        <button class="btn btn-sm btn-danger" onclick="deleteProject(${p.projectId}, '${escapeHtml(p.title)}')">Delete</button>
      </div>
    </div>
  `).join('');
}

function viewProject(id) {
    const p = allProjects.find(x => x.projectId === id);
    if (!p) return;

    document.getElementById('modalContainer').innerHTML = `
    <div class="modal-backdrop" onclick="if(event.target===this)closeModal()">
      <div class="modal" style="width:560px;">
        <div class="modal-header">
          <h3>${escapeHtml(p.title)}</h3>
          <button class="modal-close" onclick="closeModal()">&times;</button>
        </div>
        <div class="modal-body">
          <div style="display:grid; gap:10px;">
            <div><strong>Status:</strong> <span class="badge badge-${(p.status || '').toLowerCase()}">${p.status}</span></div>
            <div><strong>Created By:</strong> ${escapeHtml(p.createdBy)}</div>
            <div><strong>Team Members:</strong> ${escapeHtml(p.teamMembers)}</div>
            <div><strong>Batch:</strong> ${escapeHtml(p.batch)}</div>
            <div><strong>Files:</strong> ${p.fileCount || 0}</div>
            <div><strong>Created:</strong> ${formatDate(p.createdAt)}</div>
            ${p.dateCompleted ? `<div><strong>Completed:</strong> ${formatDate(p.dateCompleted)}</div>` : ''}
            <hr style="border:none;border-top:1px solid #eee;">
            <div><strong>Description:</strong></div>
            <div style="font-size:13px;color:#555;max-height:200px;overflow-y:auto;white-space:pre-wrap;">${escapeHtml(p.description || p.abstraction || 'No description')}</div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" onclick="closeModal()">Close</button>
        </div>
      </div>
    </div>
  `;
}

async function deleteProject(id, title) {
    if (!confirm(`Delete project "${title}"? This cannot be undone.`)) return;

    try {
        const res = await api(`/admin/projects/${id}`, { method: 'DELETE' });
        if (res.ok) {
            toast('Project deleted');
            loadProjects();
        } else {
            toast('Failed to delete project', 'error');
        }
    } catch { toast('Error deleting project', 'error'); }
}

function showAddProjectModal() {
    document.getElementById('modalContainer').innerHTML = `
    <div class="modal-backdrop" onclick="if(event.target===this)closeModal()">
      <div class="modal" style="width:520px;">
        <div class="modal-header">
          <h3>Upload Project</h3>
          <button class="modal-close" onclick="closeModal()">&times;</button>
        </div>
        <div class="modal-body">
          <div class="form-group"><label>Project Title</label><input type="text" id="projTitle" placeholder="Project title"></div>
          <div class="form-group"><label>Description</label><textarea id="projDesc" placeholder="Project description"></textarea></div>
          <div class="form-group"><label>Abstract</label><textarea id="projAbstract" placeholder="Project abstract"></textarea></div>
          <div class="form-group"><label>Batch</label><input type="text" id="projBatch" placeholder="e.g., 2022-25"></div>
          <div class="form-group"><label>Created By</label><input type="text" id="projCreatedBy" placeholder="Team/Person name"></div>
          <div class="form-group"><label>Team Members</label><input type="text" id="projTeam" placeholder="Comma-separated names"></div>
          <div class="form-group"><label>File (PDF/DOC)</label><input type="file" id="projFile" accept=".pdf,.doc,.docx,.txt"></div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" onclick="closeModal()">Cancel</button>
          <button class="btn btn-primary" onclick="uploadProject()">Upload</button>
        </div>
      </div>
    </div>
  `;
}

async function uploadProject() {
    const formData = new FormData();
    formData.append('title', document.getElementById('projTitle').value.trim());
    formData.append('description', document.getElementById('projDesc').value.trim());
    formData.append('abstraction', document.getElementById('projAbstract').value.trim());
    formData.append('batch', document.getElementById('projBatch').value.trim());
    formData.append('createdBy', document.getElementById('projCreatedBy').value.trim());
    formData.append('teamMembers', document.getElementById('projTeam').value.trim());

    const fileInput = document.getElementById('projFile');
    if (fileInput.files.length > 0) {
        formData.append('File', fileInput.files[0]);
    }

    if (!formData.get('title')) {
        toast('Please enter a project title', 'error');
        return;
    }

    try {
        const res = await fetch(`${API}/admin/projects/upload`, {
            method: 'POST',
            body: formData
        });
        if (res.ok) {
            closeModal();
            toast('Project uploaded successfully');
            loadProjects();
        } else {
            const err = await res.json();
            toast(err.error || 'Failed to upload project', 'error');
        }
    } catch { toast('Error uploading project', 'error'); }
}

// ===================== NOTIFICATIONS =====================
async function loadNotifications() {
    const container = document.getElementById('notifList');
    container.innerHTML = '<div class="spinner"></div>';

    try {
        const res = await api('/admin/notifications');
        const data = await res.json();
        allNotifications = data.notifications || [];
        document.getElementById('notifCount').textContent = allNotifications.length;
        renderNotifications(allNotifications);
    } catch (e) {
        container.innerHTML = '<div class="empty-state"><p>Failed to load notifications</p></div>';
    }
}

function renderNotifications(notifs) {
    const container = document.getElementById('notifList');
    if (!notifs.length) {
        container.innerHTML = '<div class="empty-state"><div class="icon">🔕</div><p>No notifications yet</p></div>';
        return;
    }

    container.innerHTML = notifs.map(n => `
    <div class="notif-item">
      <div class="notif-avatar">📢</div>
      <div class="notif-body">
        <div class="msg">${escapeHtml(n.message)}</div>
        <div class="meta">By ${escapeHtml(n.teacherName || 'Unknown')} • ${formatDate(n.createdAt)}</div>
      </div>
      <button class="btn-icon danger" title="Delete" onclick="deleteNotification(${n.id})">🗑️</button>
    </div>
  `).join('');
}

async function sendNotification() {
    const message = document.getElementById('notifMessage').value.trim();
    const sender = document.getElementById('notifSender').value.trim() || 'Admin';

    if (!message) {
        toast('Please enter a message', 'error');
        return;
    }

    try {
        const res = await fetch(`${API}/notifications/send`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message, teacherName: sender })
        });
        if (res.ok) {
            document.getElementById('notifMessage').value = '';
            document.getElementById('notifSender').value = '';
            toast('Notification sent successfully');
            loadNotifications();
        } else {
            toast('Failed to send notification', 'error');
        }
    } catch { toast('Error sending notification', 'error'); }
}

async function deleteNotification(id) {
    if (!confirm('Delete this notification?')) return;

    try {
        const res = await api(`/admin/notifications/${id}`, { method: 'DELETE' });
        if (res.ok) {
            toast('Notification deleted');
            loadNotifications();
        } else {
            toast('Failed to delete notification', 'error');
        }
    } catch { toast('Error deleting notification', 'error'); }
}

// ===================== TRENDING =====================
async function loadTrending() {
    const container = document.getElementById('trendingList');
    container.innerHTML = '<div class="spinner"></div>';

    try {
        const res = await api('/admin/standalone-trending-projects');
        const data = await res.json();
        const trending = data.projects || [];
        renderTrending(Array.isArray(trending) ? trending : []);
    } catch (e) {
        container.innerHTML = '<div class="empty-state"><p>Failed to load trending projects</p></div>';
    }
}

function renderTrending(items) {
    const container = document.getElementById('trendingList');
    if (!items.length) {
        container.innerHTML = '<div class="empty-state"><div class="icon">📈</div><p>No trending projects</p></div>';
        return;
    }

    container.innerHTML = items.map((t, i) => `
    <div class="trending-card">
      <div class="trending-rank">${i + 1}</div>
      <div class="trending-info">
        <h4>${escapeHtml(t.title)}</h4>
        <p>${escapeHtml(t.abstraction || t.description || '')}</p>
      </div>
      <button class="btn btn-sm btn-danger" onclick="removeTrending(${t.id})">Remove</button>
    </div>
  `).join('');
}

async function removeTrending(id) {
    if (!confirm('Remove from trending?')) return;

    try {
        const res = await api(`/admin/standalone-trending-projects/${id}`, { method: 'DELETE' });
        if (res.ok) {
            toast('Removed from trending');
            loadTrending();
        } else {
            toast('Failed to remove', 'error');
        }
    } catch { toast('Error removing trending project', 'error'); }
}

function showAddTrendingModal() {
    document.getElementById('modalContainer').innerHTML = `
    <div class="modal-backdrop" onclick="if(event.target===this)closeModal()">
      <div class="modal" style="width:480px;">
        <div class="modal-header">
          <h3>Add Trending Project</h3>
          <button class="modal-close" onclick="closeModal()">&times;</button>
        </div>
        <div class="modal-body">
          <div class="form-group"><label>Title</label><input type="text" id="trendTitle" placeholder="Project title"></div>
          <div class="form-group"><label>Abstraction</label><textarea id="trendAbstraction" placeholder="Project abstract / description"></textarea></div>
          <div class="form-group"><label>Or Upload PDF/DOC (optional)</label><input type="file" id="trendFile" accept=".pdf,.doc,.docx,.txt"></div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" onclick="closeModal()">Cancel</button>
          <button class="btn btn-primary" onclick="addTrendingProject()">Add</button>
        </div>
      </div>
    </div>
  `;
}

async function addTrendingProject() {
    const title = document.getElementById('trendTitle').value.trim();
    const abstraction = document.getElementById('trendAbstraction').value.trim();
    const fileInput = document.getElementById('trendFile');

    if (!title) {
        toast('Please enter a title', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('title', title);
    formData.append('abstraction', abstraction);
    if (fileInput.files.length > 0) {
        formData.append('File', fileInput.files[0]);
    }

    try {
        const res = await fetch(`${API}/admin/standalone-trending-projects`, {
            method: 'POST',
            body: formData
        });
        if (res.ok) {
            closeModal();
            toast('Trending project added');
            loadTrending();
        } else {
            const err = await res.json();
            toast(err.message || 'Failed to add', 'error');
        }
    } catch { toast('Error adding trending project', 'error'); }
}

// Enter key on login
document.getElementById('loginPassword').addEventListener('keypress', (e) => {
    if (e.key === 'Enter') handleLogin();
});
document.getElementById('loginEmail').addEventListener('keypress', (e) => {
    if (e.key === 'Enter') document.getElementById('loginPassword').focus();
});
