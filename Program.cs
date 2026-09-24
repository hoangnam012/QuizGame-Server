using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace GameServer
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ServerDashboard());
        }
    }

    public class ServerDashboard : Form
    {
        private Color bgMain = Color.FromArgb(20, 20, 30);
        private Color bgPanel = Color.FromArgb(35, 35, 50);
        private Color btnPurple = Color.FromArgb(128, 90, 255);
        private Color textLight = Color.WhiteSmoke;
        private Color textDim = Color.LightGray;
        private Color textGreen = Color.FromArgb(0, 255, 127);

        private Button btnStartStop;
        private Label lblStatus, lblPeersCount, lblUptime, lblMsgCount;
        private DataGridView dgvClients;
        private RichTextBox txtLogs;
        private System.Windows.Forms.Timer uptimeTimer;
        private Stopwatch stopwatch;

        private TcpListener _server;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;
        private int _activePeers = 0;

        private Dictionary<string, Room> _roomManager = new Dictionary<string, Room>();
        private Dictionary<string, NetworkStream> _clientStreams = new Dictionary<string, NetworkStream>();
        private int _msgHandled = 0;

        public ServerDashboard()
        {
            SetupModernUI();

            uptimeTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            uptimeTimer.Tick += (s, e) =>
            {
                if (stopwatch != null && stopwatch.IsRunning)
                {
                    TimeSpan ts = stopwatch.Elapsed;
                    lblUptime.Text = string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
                }
            };
        }

        private void SetupModernUI()
        {
            this.Text = "Game Server ";
            this.Size = new Size(1050, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgMain;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // --- HEADER ---
            Label lblTitle = new Label { Text = "Server", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = textLight, Location = new Point(70, 20), AutoSize = true };
            Label lblSub = new Label { Text = "Game Thi Đấu Trẻ Em - Central Registry", Font = new Font("Segoe UI", 9), ForeColor = textDim, Location = new Point(73, 50), AutoSize = true };

            lblStatus = new Label { Text = "● STOPPED", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Tomato, Location = new Point(650, 30), AutoSize = true };
            Label lblPort = new Label { Text = "Port: 8888", Font = new Font("Segoe UI", 10), ForeColor = textDim, Location = new Point(760, 30), AutoSize = true };

            btnStartStop = new Button { Text = "Khởi động", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(850, 20), Size = new Size(150, 40), BackColor = btnPurple, ForeColor = textLight, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Click += BtnStartStop_Click;

            this.Controls.Add(lblTitle); this.Controls.Add(lblSub); this.Controls.Add(lblStatus); this.Controls.Add(lblPort); this.Controls.Add(btnStartStop);

            // --- 3 STAT PANELS ---
            Panel pnlPeers = CreateCard(20, 90, 310, 100, "Clients Online");
            lblPeersCount = new Label { Text = "0", Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = btnPurple, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            pnlPeers.Controls.Add(lblPeersCount);

            Panel pnlUptime = CreateCard(350, 90, 330, 100, "Uptime Server");
            lblUptime = new Label { Text = "00:00:00", Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.DodgerBlue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            pnlUptime.Controls.Add(lblUptime);

            Panel pnlMsg = CreateCard(700, 90, 310, 100, "Tin nhắn xử lý");
            lblMsgCount = new Label { Text = "0", Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.LightSeaGreen, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            pnlMsg.Controls.Add(lblMsgCount);

            this.Controls.Add(pnlPeers); this.Controls.Add(pnlUptime); this.Controls.Add(pnlMsg);

            // --- DATAGRIDVIEW ---
            Label lblGridTitle = new Label { Text = "Clients đang Online", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = textLight, Location = new Point(20, 215), AutoSize = true };
            this.Controls.Add(lblGridTitle);

            dgvClients = new DataGridView
            {
                Location = new Point(20, 250),
                Size = new Size(990, 200),
                BackgroundColor = bgPanel,
                ForeColor = textLight,
                GridColor = Color.FromArgb(50, 50, 70),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            dgvClients.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 35);
            dgvClients.ColumnHeadersDefaultCellStyle.ForeColor = textDim;
            dgvClients.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            dgvClients.DefaultCellStyle.BackColor = bgPanel;
            dgvClients.DefaultCellStyle.SelectionBackColor = btnPurple;
            dgvClients.DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Regular);

            dgvClients.Columns.Add("ID", "#");
            dgvClients.Columns[0].Width = 40;
            dgvClients.Columns.Add("Username", "Username");
            dgvClients.Columns.Add("IP", "Địa chỉ IP");
            dgvClients.Columns.Add("TimeIn", "Thời gian vào"); // Đã khôi phục
            dgvClients.Columns.Add("TimeOut", "Thời gian ra");  // Đã khôi phục
            dgvClients.Columns.Add("Status", "Trạng thái");
            dgvClients.Columns.Add("Action", "Hành động");
            this.Controls.Add(dgvClients);

            // --- LOG CONSOLE ---
            Label lblLogTitle = new Label { Text = "Server Log Console", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = textLight, Location = new Point(20, 475), AutoSize = true };
            this.Controls.Add(lblLogTitle);

            txtLogs = new RichTextBox
            {
                Location = new Point(20, 510),
                Size = new Size(990, 180),
                BackColor = Color.FromArgb(10, 10, 15),
                ForeColor = textGreen,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };
            this.Controls.Add(txtLogs);
        }

        private Panel CreateCard(int x, int y, int w, int h, string title)
        {
            Panel p = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = bgPanel };
            Label l = new Label { Text = title, ForeColor = textDim, Font = new Font("Segoe UI", 9), Location = new Point(10, 10), AutoSize = true };
            p.Controls.Add(l);
            return p;
        }

        private async void BtnStartStop_Click(object? sender, EventArgs e)
        {
            if (!_isRunning)
            {
                _isRunning = true;
                btnStartStop.Text = "Dừng Server";
                lblStatus.Text = "● RUNNING";
                lblStatus.ForeColor = textGreen;

                stopwatch = Stopwatch.StartNew();
                uptimeTimer.Start();
                Log("[INFO] ====== Tracker Server khởi động thành công ======");
                Log("[INFO] Lắng nghe tại cổng: 8888");

                _cancellationTokenSource = new CancellationTokenSource();
                _server = new TcpListener(IPAddress.Any, 8888);
                _server.Start();

                try
                {
                    while (_isRunning)
                    {
                        TcpClient client = await _server.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                        HandleClient(client);
                    }
                }
                catch (OperationCanceledException) { }
            }
            else
            {
                _isRunning = false;
                btnStartStop.Text = "Khởi động";
                lblStatus.Text = "● STOPPED";
                lblStatus.ForeColor = Color.Tomato;

                uptimeTimer.Stop();
                stopwatch.Stop();
                _cancellationTokenSource?.Cancel();
                _server?.Stop();
                Log("[WARN] ====== Server đã dừng hoạt động ======");
            }
        }

        private void HandleClient(TcpClient client)
        {
            string ip = ((IPEndPoint)client.Client.RemoteEndPoint!).ToString();
            string timeIn = DateTime.Now.ToString("HH:mm:ss");
            string defaultUser = "Unknown_Kid";

            Log($"[CONN] Client mới kết nối từ {ip}");

            this.Invoke((MethodInvoker)delegate
            {
                dgvClients.Rows.Add(dgvClients.Rows.Count + 1, defaultUser, ip, timeIn, "-", "Online", "Đang trong phòng");
                _activePeers++;
                lblPeersCount.Text = _activePeers.ToString();
            });

            Task.Run(async () =>
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    _clientStreams[ip] = stream;
                    byte[] buffer = new byte[1024];

                    while (client.Connected)
                    {
                        if (client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0)
                        {
                            break;
                        }
                        if (stream.DataAvailable)
                        {
                            int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytes > 0)
                            {
                                string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, bytes);
                                ProcessMessage(ip, msg, stream);
                            }
                        }
                        await Task.Delay(100);
                    }
                }
                catch { }
                finally
                {
                    string timeOut = DateTime.Now.ToString("HH:mm:ss");
                    this.Invoke((MethodInvoker)delegate
                    {
                        UpdateClientDisconnect(ip, timeOut);
                        _activePeers = Math.Max(0, _activePeers - 1);
                        lblPeersCount.Text = _activePeers.ToString();
                        Log($"[DISC] Client {ip} đã ngắt kết nối.");

                        string roomToRemove = "";
                        foreach (var r in _roomManager)
                        {
                            if (r.Value.HostIP == ip)
                            {
                                roomToRemove = r.Key;
                                break;
                            }
                        }

                        if (roomToRemove != "")
                        {
                            _roomManager.Remove(roomToRemove);
                            Log($"[ROOM] Đã giải tán phòng {roomToRemove} vì Host đã thoát game!");
                        }
                    });
                }
            });
        }

        private void ProcessMessage(string ip, string msg, NetworkStream stream)
        {
            msg = msg.Trim().Replace("\0", "");
            Log($"[DEBUG] Vừa nhận được từ {ip}: {msg}");
            this.Invoke((MethodInvoker)delegate
            {
            _msgHandled++;
            lblMsgCount.Text = _msgHandled.ToString();

            foreach (DataGridViewRow row in dgvClients.Rows)
            {
                if (row.Cells[2].Value.ToString() == ip && row.Cells[5].Value.ToString() == "Online")
                {
                    if (msg.StartsWith("LOGIN:"))
                    {
                        string user = msg.Substring(6).Trim();
                        row.Cells[1].Value = user;
                        Log($"[LOGIN] {user} ({ip}) online.");

                        SendToClient(stream, "LOGIN_SUCCESS"); // Dùng hàm mới rút gọn code!
                        Log($"[SERVER -> {user}] Đã phản hồi: LOGIN_SUCCESS");
                    }
                    // ==========================================
                    // THÊM MODULE XỬ LÝ TẠO PHÒNG Ở ĐÂY
                    // ==========================================
                    else if (msg.StartsWith("CREATE_ROOM:"))
                    {
                        // msg có dạng: CREATE_ROOM:111222:Lop3:ToanHoc
                        string[] parts = msg.Split(':');

                        if (parts.Length == 4)
                        {
                            string roomCode = parts[1];
                            string className = parts[2];
                            string subject = parts[3];

                            // 1. Kiểm tra xem mã code này có ai xài chưa
                            if (_roomManager.ContainsKey(roomCode))
                            {
                                SendToClient(stream, "ROOM_EXISTS");
                                Log($"[CẢNH BÁO] {ip} cố tạo phòng {roomCode} nhưng mã này đã tồn tại!");
                            }
                            else
                            {
                                string hostName = GetUsernameByIP(ip);

                                Room newRoom = new Room
                                {
                                    RoomCode = roomCode,
                                    ClassName = className,
                                    Subject = subject,
                                    HostIP = ip
                                };

                                newRoom.Players.Add(hostName); // Thêm tên host vào danh sách
                                _roomManager.Add(roomCode, newRoom);
                                // ====================================================

                                SendToClient(stream, "CREATE_SUCCESS");
                                row.Cells[6].Value = $"Đang Host phòng: {roomCode}";
                                Log($"[ROOM] Tạo phòng thành công! Mã: {roomCode} | Host: {hostName}");

                                string initialPlayerList = string.Join(",", _roomManager[roomCode].Players);
                                SendToClient(stream, $"LOBBY_UPDATE:{initialPlayerList}");
                            }
                        }
                    }
                    else if (msg.StartsWith("JOIN_ROOM:"))
                    {
                        string[] parts = msg.Split(':');
                        if (parts.Length == 2)
                        {
                            string roomCode = parts[1].Trim();

                            if (_roomManager.ContainsKey(roomCode))
                            {
                                string joinerName = GetUsernameByIP(ip);
                                _roomManager[roomCode].Players.Add(joinerName);

                                SendToClient(stream, "JOIN_SUCCESS");

                                row.Cells[6].Value = $"Đang chơi phòng: {roomCode}";
                                Log($"[ROOM] {row.Cells[1].Value} (IP: {ip}) đã chui vào phòng {roomCode} thành công!");

                                string playerList = string.Join(",", _roomManager[roomCode].Players);

                                // Dùng hàm Broadcast có sẵn của ông để réo tên cả phòng
                                BroadcastToRoom(roomCode, $"LOBBY_UPDATE:{playerList}");
                            }
                            else
                            {
                                SendToClient(stream, "ROOM_NOT_FOUND");
                                Log($"[CẢNH BÁO] {ip} tìm phòng {roomCode} nhưng phòng không tồn tại!");
                            }
                        }
                    }
                    else if (msg.StartsWith("GET_LOBBY:"))
                    {
                        string reqCode = msg.Split(':')[1].Trim();
                        if (_roomManager.ContainsKey(reqCode))
                        {
                            string playerList = string.Join(",", _roomManager[reqCode].Players);

                            SendToClient(stream, $"LOBBY_UPDATE:{playerList}");
                            Log($"[LOBBY] Đã gửi danh sách người chơi phòng {reqCode} cho {ip}");
                        }
                    }
                    else if (msg.StartsWith("START_GAME:"))
                    {
                        try
                        {
                            string reqCode = msg.Split(':')[1].Trim();
                            if (_roomManager[reqCode].HostIP == ip)
                            {
                                // Bắn lệnh báo hiệu cho cả phòng biết
                                BroadcastToRoom(reqCode, $"GAME_STARTED:{reqCode}");

                                // Chỉ chạy 1 Vòng Lặp duy nhất cho 1 phòng
                                _ = Task.Run(() => StartGameLoop(reqCode));

                                Log($"[GAME] Host ({ip}) ĐÃ BẮT ĐẦU VÀO TRẬN phòng {reqCode}!");
                            }
                            else
                            {
                                // Nếu không phải Host mà dám bấm Play thì block luôn
                                Log($"[CẢNH BÁO] {ip} bấm Play phòng {reqCode} nhưng KHÔNG PHẢI LÀ HOST!");
                                // Nếu thích thì bắn thêm câu chửi về cho nó: SendToClient(stream, "NOT_HOST");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[LỖI START_GAME] {ex.Message}");
                        }
                    }
                    else if (msg.StartsWith("ACTION:"))
                    {
                        row.Cells[6].Value = msg.Substring(7).Trim();
                    }
                    else if (msg.StartsWith("ANSWER:"))
                    {
                        // msg từ Unity gửi lên có dạng: ANSWER:111111:nam:2
                        string[] parts = msg.Split(':');
                        if (parts.Length == 4)
                        {
                            string ansRoomCode = parts[1];
                            string ansUser = parts[2];
                            string ansIndex = parts[3];

                            if (_roomManager.ContainsKey(ansRoomCode))
                            {
                                Room r = _roomManager[ansRoomCode];

                                // 1. Chỉ nhận đáp án nếu đồng hồ 10s vẫn đang đếm
                                if (r.IsAcceptingAnswers)
                                {
                                    // 2. Tạo giỏ điểm nếu người chơi này chưa có
                                    if (!r.PlayerScores.ContainsKey(ansUser))
                                    {
                                        r.PlayerScores[ansUser] = 0;
                                    }

                                    // 3. SO SÁNH ĐÁP ÁN ĐÚNG/SAI
                                    if (ansIndex.Trim() == r.CurrentAnswer.Trim())
                                    {
                                        // Tính thời gian chênh lệch để cộng điểm Kahoot
                                        double timeTaken = (DateTime.Now - r.QuestionStartTime).TotalSeconds;
                                        int pointsToAdd = 1000 - (int)(timeTaken * 100);
                                        if (pointsToAdd < 100) pointsToAdd = 100;

                                        r.PlayerScores[ansUser] += pointsToAdd;
                                        Log($"[GAME] {ansUser} ĐÚNG! +{pointsToAdd} điểm. Tổng: {r.PlayerScores[ansUser]}");
                                        SendToClient(stream, $"ANSWER_RESULT:CORRECT:{pointsToAdd}");
                                    }
                                    else
                                    {
                                        Log($"[GAME] {ansUser} SAI! Không có điểm. Tổng: {r.PlayerScores[ansUser]}");

                                        // ==========================================
                                        // THÊM DÒNG NÀY: Báo tin buồn về cho riêng người vừa bấm
                                        // ==========================================
                                        SendToClient(stream, "ANSWER_RESULT:WRONG:0");
                                    }
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            });
        }
        private void BroadcastToRoom(string roomCode, string message)
        {
            if (!_roomManager.ContainsKey(roomCode)) return;
            List<string> playersInRoom = _roomManager[roomCode].Players;

            // Bắt buộc phải có Invoke để không bị crash ngầm khi đọc DataGridView
            this.Invoke((MethodInvoker)delegate
            {
                foreach (DataGridViewRow r in dgvClients.Rows)
                {
                    string ip = r.Cells[2].Value.ToString();
                    string name = r.Cells[1].Value.ToString();

                    if (r.Cells[5].Value.ToString() == "Online" && playersInRoom.Contains(name))
                    {
                        if (_clientStreams.ContainsKey(ip))
                        {
                            SendToClient(_clientStreams[ip], message);
                        }
                    }
                }
            });
        }
        private async Task StartGameLoop(string roomCode)
        {
            try
            {
                Room room = _roomManager[roomCode];
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = System.IO.Path.Combine(baseDir, "Data", room.ClassName, $"{room.Subject}.txt");

                List<string> questions = new List<string>();
                if (System.IO.File.Exists(filePath))
                {
                    // Đọc toàn bộ các dòng trong file .txt bỏ vào danh sách
                    questions = new List<string>(System.IO.File.ReadAllLines(filePath));
                    Log($"[GAME] Đã nạp thành công {questions.Count} câu hỏi môn {room.Subject} cho phòng {roomCode}");
                }
                else
                {
                    Log($"[CẢNH BÁO] Không tìm thấy đề thi: {filePath}. Sẽ dùng câu hỏi dự phòng!");
                    // Câu hỏi backup nếu lỡ quên tạo file .txt
                    questions.Add("Lỗi không tìm thấy đề thi, vui lòng báo Admin!|A. Ok|B. Dạ|C. Vâng|D. Biết rồi|1");
                }


                for (int i = 0; i < questions.Count; i++)
                {
                    string[] qData = questions[i].Split('|');
                    room.CurrentAnswer = qData[5];
                    room.QuestionStartTime = DateTime.Now;
                    room.IsAcceptingAnswers = true;

                    // MỚI SỬA: Gửi kèm luôn qData[5] (đáp án đúng) xuống ở cuối gói tin QUESTION
                    string packet = $"QUESTION:{qData[0]}|{qData[1]}|{qData[2]}|{qData[3]}|{qData[4]}|{qData[5]}";
                    BroadcastToRoom(roomCode, packet);
                    Log($"[GAME] Phòng {roomCode} Đang chạy câu {i + 1}");

                    // TRỌNG TÀI BẮT ĐẦU ĐỢI 10 GIÂY
                    await Task.Delay(10000);
                    room.IsAcceptingAnswers = false;

                    // 4. KIỂM TRA HIỂN THỊ BẢNG XẾP HẠNG MỖI 5 CÂU HOẶC KHI HẾT CÂU HỎI
                    if ((i + 1) % 5 == 0 || i == questions.Count - 1)
                    {
                        Log($"[GAME] Đang tính toán Bảng Xếp Hạng cho phòng {roomCode}...");

                        // BƯỚC 1: Sắp xếp điểm số từ cao xuống thấp
                        // Chuyển Dictionary thành danh sách để dễ sort
                        var sortedScores = room.PlayerScores.OrderByDescending(p => p.Value).ToList();

                        // BƯỚC 2: Gói data thành chuỗi để gửi đi
                        // Định dạng gửi: LEADERBOARD:Tên1-Điểm1|Tên2-Điểm2|Tên3-Điểm3
                        List<string> scoreStrings = new List<string>();
                        foreach (var p in sortedScores)
                        {
                            scoreStrings.Add($"{p.Key}:{p.Value}");
                        }

                        // Nếu phòng chưa ai có điểm (trả lời sai hết), gửi danh sách trống
                        string leaderboardData = scoreStrings.Count > 0 ? string.Join("|", scoreStrings) : "EMPTY";
                        string lbPacket = $"LEADERBOARD:{leaderboardData}";

                        // BƯỚC 3: Bắn gói tin Bảng Xếp Hạng cho tất cả Client
                        BroadcastToRoom(roomCode, lbPacket);
                        Log($"[GAME] Đã gửi Bảng Xếp Hạng: {lbPacket}");

                        // BƯỚC 4: Dừng 5 giây để Client ngắm Bảng Xếp Hạng trước khi qua câu tiếp theo
                        await Task.Delay(5000);
                    }
                } // Kết thúc vòng lặp for (hết câu hỏi)

                // KHI CHẠY XONG HẾT VÒNG LẶP FOR (HẾT ĐỀ THI)
                BroadcastToRoom(roomCode, "GAME_OVER");
                Log($"[GAME] Phòng {roomCode} đã kết thúc ván chơi!");
            }
            catch (Exception ex)
            {
                Log($"[LỖI LOOP] {ex.Message} \n {ex.StackTrace}");
            }
        }

        private void UpdateClientDisconnect(string ip, string timeOut)
        {
            for (int i = 0; i < dgvClients.Rows.Count; i++)
            {
                // Chỉ cập nhật row nào đang "Online" của IP đó
                if (dgvClients.Rows[i].Cells[2].Value.ToString() == ip && dgvClients.Rows[i].Cells[5].Value.ToString() == "Online")
                {
                    dgvClients.Rows[i].Cells[4].Value = timeOut; // Cập nhật cột Thời gian ra
                    dgvClients.Rows[i].Cells[5].Value = "Offline";
                    dgvClients.Rows[i].Cells[6].Value = "Đã ngắt kết nối";
                    dgvClients.Rows[i].DefaultCellStyle.ForeColor = Color.DimGray;
                    break;
                }
            }
        }

        private void Log(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(Log), message);
                return;
            }
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            txtLogs.ScrollToCaret();
        }
        private void SendToClient(NetworkStream stream, string message)
        {
            try
            {
                // Thêm \n vào cuối để chống dính chùm tin nhắn TCP
                byte[] data = System.Text.Encoding.UTF8.GetBytes(message + "\n");
                stream.Write(data, 0, data.Length);
            }
            catch (System.Exception ex)
            {
                Log($"[LỖI GỬI] {ex.Message}");
            }
        }
        private string GetUsernameByIP(string ip)
        {
            foreach (DataGridViewRow row in dgvClients.Rows)
            {
                if (row.Cells[2].Value.ToString() == ip && row.Cells[5].Value.ToString() == "Online")
                {
                    return row.Cells[1].Value.ToString();
                }
            }
            return "Unknown";
        }
    }
}