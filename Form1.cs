using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace ChrodAnalyzer
{
    public class ChordAnalyzer
    {
        public static string AnalyzeChord(List<Note> notes)
        {
            if (notes.Count == 0)
                return "";

            // 提取音高（忽略八度）
            var pitches = notes.Select(n => n.NoteNumber % 12).Distinct().OrderBy(p => p).ToList();

            if (pitches.Count == 0)
                return "";

            // 计算音程
            var intervals = new List<int>();
            for (int i = 0; i < pitches.Count; i++)
            {
                int interval = (pitches[(i + 1) % pitches.Count] - pitches[i] + 12) % 12;
                intervals.Add(interval);
            }

            // 识别和弦类型
            return IdentifyChord(pitches, intervals);
        }

        private static string IdentifyChord(List<int> pitches, List<int> intervals)
        {
            // 国际符号和弦识别（包括转位和弦）
            if (pitches.Count == 0)
                return "";

            // 找到最低音（低音）
            int bassNote = pitches.Min();

            // 识别原位和弦的根音和类型
            (int root, string chordType, bool isUncertain) = IdentifyOriginalChord(pitches);
            
            // 智能选择音名表示
            bool preferFlat = ShouldPreferFlat(root, bassNote, chordType);
            string rootNote = GetNoteName(root, preferFlat);
            
            // 构建和弦名称
            string chordName = rootNote + chordType;

            // 确定转位状态并添加转位标记
            if (bassNote != root)
            {
                string bassNoteName = GetNoteName(bassNote, preferFlat);
                chordName += "/" + bassNoteName;
            }

            // 标记不确定的和弦
            if (isUncertain)
            {
                chordName += "?";
            }

            return chordName;
        }

        private static bool ShouldPreferFlat(int root, int bassNote, string chordType)
        {
            // 根据和弦类型和转位情况智能选择升半音或降半音
            int[] flatTendingNotes = { 1, 3, 6, 8, 10 }; // Db, Eb, Gb, Ab, Bb
            
            // 检查根音是否倾向于降半音
            if (flatTendingNotes.Contains(root % 12))
                return true;
            
            // 检查低音是否倾向于降半音
            if (flatTendingNotes.Contains(bassNote % 12))
                return true;
            
            // 默认使用升半音
            return false;
        }

        private static (int root, string chordType, bool isUncertain) IdentifyOriginalChord(List<int> pitches)
        {
            // 尝试所有可能的根音，找到最合适的和弦
            foreach (var candidateRoot in pitches)
            {
                List<int> rootIntervals = new List<int>();
                
                foreach (var pitch in pitches)
                {
                    // 计算相对于候选根音的音程
                    int interval = (pitch - candidateRoot + 12) % 12;
                    if (interval > 0) // 排除根音本身
                        rootIntervals.Add(interval);
                }
                rootIntervals.Sort();

                // 识别和弦类型
                string chordType = IdentifyChordType(rootIntervals);
                if (chordType != "")
                {
                    return (candidateRoot, chordType, false);
                }
            }

            // 无法识别的和弦，返回一个确定的和弦类型并标记为不确定
            int root = pitches.Min();
            return (root, "", true);
        }

        private static string IdentifyChordType(List<int> intervals)
        {
            // 标准和弦类型识别
            if (intervals.Contains(4) && intervals.Contains(7))
            {
                if (intervals.Contains(11))
                    return "7";
                else if (intervals.Contains(10))
                    return "maj7";
                else
                    return "";
            }
            else if (intervals.Contains(3) && intervals.Contains(7))
            {
                if (intervals.Contains(10))
                    return "m7";
                else if (intervals.Contains(11))
                    return "m7b5";
                else
                    return "m";
            }
            else if (intervals.Contains(3) && intervals.Contains(6))
            {
                return "°";
            }
            else if (intervals.Contains(4) && intervals.Contains(8))
            {
                return "+";
            }
            else if (intervals.Contains(2) && intervals.Contains(7))
            {
                return "sus2";
            }
            else if (intervals.Contains(5) && intervals.Contains(7))
            {
                return "sus4";
            }
            else if (intervals.Contains(4) && intervals.Contains(9))
            {
                return "add9";
            }
            else if (intervals.Contains(3) && intervals.Contains(9))
            {
                return "madd9";
            }
            // 处理单音程情况
            else if (intervals.Contains(4))
            {
                return "";
            }
            else if (intervals.Contains(3))
            {
                return "m";
            }
            else if (intervals.Contains(7))
            {
                return "";
            }
            else if (intervals.Contains(9))
            {
                return "add9";
            }
            // 处理其他情况
            else if (intervals.Count > 0)
            {
                int avgInterval = intervals.Sum() / intervals.Count;
                return avgInterval <= 3 ? "m" : "";
            }
            else
            {
                return "";
            }
        }

        public static string GetNoteName(int pitch)
        {
            string[] sharpNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string[] flatNames = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
            
            // 默认使用升半音
            return sharpNames[pitch % 12];
        }

        public static string GetNoteName(int pitch, bool preferFlat)
        {
            string[] sharpNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string[] flatNames = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
            
            // 根据参数选择升半音或降半音
            if (preferFlat)
                return flatNames[pitch % 12];
            else
                return sharpNames[pitch % 12];
        }

        public static List<(long time, string chord)> AnalyzeChords(List<Note> notes)
        {
            var chords = new List<(long time, string chord)>();
            if (notes.Count == 0)
                return chords;

            // 按时间分组
            var notesByTime = notes.GroupBy(n => n.Time).OrderBy(g => g.Key);

            foreach (var group in notesByTime)
            {
                string chord = AnalyzeChord(group.ToList());
                if (!string.IsNullOrEmpty(chord))
                {
                    chords.Add((group.Key, chord));
                }
            }

            return chords;
        }
    }

    public partial class Form1 : Form
    {
        private MidiFile? midiFile = null;
        private List<Note>? allNotes = null;
        private List<TrackChunk>? tracks = null;
        private List<(long time, string chord)>? chords = null;
        private double zoomLevel = 1.0;
        private double horizontalOffset = 0.0; // 水平偏移量
        private double verticalOffset = 0.0; // 垂直偏移量
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;

        public Form1()
        {
            this.Text = "MIDI和弦分析器";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            InitializeMenu();
            InitializeTimeline();
            
            // 添加鼠标滚轮事件处理
            var timelinePanel = this.Controls.Find("timelinePanel", true).FirstOrDefault() as Panel;
            var pianoRollPanel = this.Controls.Find("pianoRollPanel", true).FirstOrDefault() as Panel;
            
            if (timelinePanel != null)
            {
                timelinePanel.MouseWheel += TimelinePanel_MouseWheel;
            }
            
            if (pianoRollPanel != null)
            {
                pianoRollPanel.MouseWheel += TimelinePanel_MouseWheel;
            }
        }

        private void InitializeMenu()
        {
            MenuStrip menuStrip = new MenuStrip();
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("文件");
            menuStrip.Items.Add(fileMenu);

            ToolStripMenuItem openMenuItem = new ToolStripMenuItem("打开MIDI文件...");
            openMenuItem.Click += OpenMidiFile;
            fileMenu.DropDownItems.Add(openMenuItem);
        }

        private void InitializeTimeline()
        {
            // 创建钢琴窗卷帘区域（全屏）
            Panel pianoRollPanel = new Panel();
            pianoRollPanel.Dock = DockStyle.Fill;
            pianoRollPanel.BackColor = Color.FromArgb(40, 40, 40);
            pianoRollPanel.Name = "pianoRollPanel";
            this.Controls.Add(pianoRollPanel);

            // 创建时间轴和和弦显示区域（顶部条）
            Panel timelinePanel = new Panel();
            timelinePanel.Dock = DockStyle.Top;
            timelinePanel.Height = 80;
            timelinePanel.BackColor = Color.FromArgb(40, 40, 40);
            timelinePanel.Name = "timelinePanel";
            this.Controls.Add(timelinePanel);
        }

        private void OpenMidiFile(object? sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // 读取MIDI文件
                    midiFile = MidiFile.Read(openFileDialog.FileName);
                    
                    // 提取所有音符
                    allNotes = new List<Note>();
                    tracks = midiFile.Chunks.OfType<TrackChunk>().ToList();
                    foreach (var track in tracks)
                    {
                        var notes = track.GetNotes();
                        allNotes.AddRange(notes);
                    }

                    // 分析和弦
                    chords = ChordAnalyzer.AnalyzeChords(allNotes);

                    // 显示MIDI文件信息
                    MessageBox.Show($"成功读取MIDI文件\n音轨数量: {tracks.Count}\n音符数量: {allNotes.Count}\n和弦数量: {chords.Count}", "信息", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // 绘制时间轴
                    DrawTimeline();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取MIDI文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DrawTimeline()
        {
            Panel timelinePanel = this.Controls.Find("timelinePanel", true).FirstOrDefault() as Panel;
            Panel pianoRollPanel = this.Controls.Find("pianoRollPanel", true).FirstOrDefault() as Panel;
            
            if (timelinePanel == null || pianoRollPanel == null || midiFile == null || allNotes == null || tracks == null)
                return;

            // 计算总时间
            long totalTicks = 0;
            foreach (var note in allNotes)
            {
                if (note.Time + note.Length > totalTicks)
                    totalTicks = note.Time + note.Length;
            }

            // 绘制时间轴和和弦
            DrawTimelinePanel(timelinePanel, totalTicks);
            
            // 绘制钢琴窗卷帘面板
            DrawPianoRollPanel(pianoRollPanel, totalTicks);
        }

        private void DrawTimelinePanel(Panel panel, long totalTicks)
        {
            int panelHeight = panel.Height;
            int panelWidth = panel.Width;

            // 创建双缓冲，避免闪烁
            Bitmap bitmap = new Bitmap(panelWidth, panelHeight);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // 绘制深色背景
                g.Clear(Color.FromArgb(40, 40, 40));
                Pen pen = new Pen(Color.White);
                Pen darkPen = new Pen(Color.FromArgb(60, 60, 60));
                Font font = new Font("Arial", 8);

                // 绘制水平时间轴
                g.DrawLine(pen, 50, panelHeight - 20, panelWidth - 20, panelHeight - 20);

                // 绘制时间刻度
                if (totalTicks > 0)
                {
                    int majorDivisions = 24; // 主要刻度数量
                    int minorDivisions = 4;  // 每个主要刻度之间的次要刻度数量
                    
                    for (int i = 0; i <= majorDivisions * minorDivisions; i++)
                    {
                        double scaledPosition = (double)i / (majorDivisions * minorDivisions) * zoomLevel - horizontalOffset;
                        if (scaledPosition <= 1.0 && scaledPosition >= 0)
                        {
                            int x = 50 + (int)(scaledPosition * (panelWidth - 70));
                            if (x >= 50)
                            {
                                if (i % minorDivisions == 0)
                                {
                                    // 主要刻度
                                    g.DrawLine(pen, x, panelHeight - 20, x, panelHeight - 10);
                                    // 绘制时间标签
                                    string timeLabel = $"{i / minorDivisions + 1}";
                                    g.DrawString(timeLabel, font, Brushes.White, x - 5, panelHeight - 35);
                                }
                                else
                                {
                                    // 次要刻度
                                    g.DrawLine(darkPen, x, panelHeight - 20, x, panelHeight - 15);
                                }
                            }
                        }
                    }
                }

                // 绘制和弦标注
                if (chords != null && chords.Count > 0 && totalTicks > 0)
                {
                    Font chordFont = new Font("Arial", 9, FontStyle.Bold);
                    Brush chordBrush = Brushes.Red;
                    Brush chordBackgroundBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 200));

                    foreach (var chord in chords)
                    {
                        double chordPosition = (double)chord.time / totalTicks * zoomLevel - horizontalOffset;
                        if (chordPosition <= 1.0 && chordPosition >= 0)
                        {
                            int chordX = 50 + (int)(chordPosition * (panelWidth - 70));
                            if (chordX >= 50)
                            {
                                int chordY = 10;

                                // 测量和弦文本宽度
                                SizeF textSize = g.MeasureString(chord.chord, chordFont);

                                // 绘制背景矩形
                                g.FillRectangle(chordBackgroundBrush, 
                                    chordX - 5, chordY - 2, 
                                    textSize.Width + 10, textSize.Height + 4);

                                // 绘制和弦文本
                                g.DrawString(chord.chord, chordFont, chordBrush, chordX, chordY);

                                // 绘制连接线
                                g.DrawLine(pen, chordX + (int)(textSize.Width / 2), chordY + (int)textSize.Height + 5, 
                                          chordX + (int)(textSize.Width / 2), panelHeight - 20);
                            }
                        }
                    }

                    chordFont.Dispose();
                    chordBackgroundBrush.Dispose();
                }

                pen.Dispose();
                darkPen.Dispose();
                font.Dispose();
            }

            // 将绘制结果显示到面板
            using (Graphics g = panel.CreateGraphics())
            {
                g.DrawImage(bitmap, 0, 0);
            }

            bitmap.Dispose();
        }

        private void DrawPianoRollPanel(Panel panel, long totalTicks)
        {
            int panelHeight = panel.Height;
            int panelWidth = panel.Width;

            // 创建双缓冲，避免闪烁
            Bitmap bitmap = new Bitmap(panelWidth, panelHeight);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // 绘制深色背景
                g.Clear(Color.FromArgb(40, 40, 40));
                Pen pen = new Pen(Color.Gray);
                Pen darkPen = new Pen(Color.FromArgb(60, 60, 60));
                Pen whitePen = new Pen(Color.White);
                Font font = new Font("Arial", 8);

                // 固定音高范围：C0到C6 (MIDI音符24到84)
                int minNote = 24; // C0
                int maxNote = 84; // C6
                int noteRange = maxNote - minNote + 1;
                int noteHeight = Math.Max(12, panelHeight / noteRange);

                // 绘制钢琴键背景
                for (int i = 0; i < noteRange; i++)
                {
                    int note = maxNote - i;
                    int noteY = (int)(i * noteHeight - verticalOffset * panelHeight);
                    
                    // 只绘制在面板内的键
                    if (noteY + noteHeight > 0 && noteY < panelHeight)
                    {
                        // 绘制黑白键
                        if (IsBlackKey(note))
                        {
                            g.FillRectangle(Brushes.Black, 30, noteY, 20, noteHeight);
                        }
                        else
                        {
                            g.FillRectangle(Brushes.White, 30, noteY, 20, noteHeight);
                            g.DrawRectangle(pen, 30, noteY, 20, noteHeight);
                        }

                        // 绘制音名（仅在白键上）
                        if (!IsBlackKey(note))
                        {
                            string noteName = ChordAnalyzer.GetNoteName(note % 12);
                            int octave = note / 12 - 1;
                            string noteLabel = $"{noteName}{octave}";
                            g.DrawString(noteLabel, font, Brushes.White, 5, noteY + (noteHeight - 12) / 2);
                        }
                    }
                }

                // 绘制网格背景
                int gridWidth = panelWidth - 70;
                int gridHeight = panelHeight;
                
                // 垂直线（时间网格）
                if (totalTicks > 0)
                {
                    int majorDivisions = 24; // 主要刻度数量
                    int minorDivisions = 4;  // 每个主要刻度之间的次要刻度数量
                    
                    for (int i = 0; i <= majorDivisions * minorDivisions; i++)
                    {
                        double scaledPosition = (double)i / (majorDivisions * minorDivisions) * zoomLevel;
                        if (scaledPosition <= 1.0)
                        {
                            int x = 50 + (int)(scaledPosition * gridWidth);
                            if (i % minorDivisions == 0)
                            {
                                // 主要刻度
                                g.DrawLine(whitePen, x, 0, x, gridHeight);
                                // 绘制时间标签
                                string timeLabel = $"{i / minorDivisions + 1}";
                                g.DrawString(timeLabel, font, Brushes.White, x - 5, 2);
                            }
                            else
                            {
                                // 次要刻度
                                g.DrawLine(darkPen, x, 0, x, gridHeight);
                            }
                        }
                    }
                }

                // 水平线（音高网格）
                for (int i = 0; i <= noteRange; i++)
                {
                    int y = i * noteHeight;
                    g.DrawLine(darkPen, 50, y, panelWidth - 20, y);
                }

                // 绘制边框
                g.DrawLine(whitePen, 50, 0, panelWidth - 20, 0);
                g.DrawLine(whitePen, 50, 0, 50, panelHeight);
                g.DrawLine(whitePen, 50, panelHeight, panelWidth - 20, panelHeight);

                // 绘制音符
                if (totalTicks > 0)
                {
                    foreach (var note in allNotes)
                    {
                        // 只显示在C0到C6范围内的音符
                        if (note.NoteNumber >= minNote && note.NoteNumber <= maxNote)
                        {
                            double startTime = (double)note.Time / totalTicks * zoomLevel - horizontalOffset;
                            double duration = (double)note.Length / totalTicks * zoomLevel;
                            if (startTime <= 1.0 && startTime + duration > 0)
                            {
                                int noteX = 50 + (int)(startTime * gridWidth);
                                int noteWidth = Math.Max(2, (int)(duration * gridWidth));
                                // 确保音符不超出面板边界
                                noteWidth = Math.Min(noteWidth, panelWidth - 20 - noteX);
                                if (noteX >= 50)
                                {
                                    int noteY = (int)((maxNote - note.NoteNumber) * noteHeight - verticalOffset * panelHeight);
                                    int noteRectHeight = noteHeight - 2;

                                    // 只绘制在面板内的音符
                                    if (noteY + noteRectHeight > 0 && noteY < panelHeight)
                                    {
                                        // 绘制音符矩形
                                        Brush brush = new SolidBrush(Color.FromArgb(150, 0, 128, 255));
                                        g.FillRectangle(brush, noteX, noteY + 1, noteWidth, noteRectHeight);
                                        g.DrawRectangle(whitePen, noteX, noteY + 1, noteWidth, noteRectHeight);
                                    }
                                }
                            }
                        }
                    }
                }

                pen.Dispose();
                darkPen.Dispose();
                whitePen.Dispose();
                font.Dispose();
            }

            // 将绘制结果显示到面板
            using (Graphics g = panel.CreateGraphics())
            {
                g.DrawImage(bitmap, 0, 0);
            }

            bitmap.Dispose();
        }

        private bool IsBlackKey(int noteNumber)
        {
            int note = noteNumber % 12;
            return note == 1 || note == 3 || note == 6 || note == 8 || note == 10;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (midiFile != null)
            {
                DrawTimeline();
            }
        }

        private void TimelinePanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                // 计算缩放因子
                double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
                zoomLevel *= zoomFactor;
                
                // 限制缩放范围
                zoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, zoomLevel));
                
                // 重绘时间轴
                if (midiFile != null)
                {
                    DrawTimeline();
                }
            }
            else if (ModifierKeys == Keys.Shift)
            {
                // 调整水平偏移量
                double offsetFactor = e.Delta > 0 ? -0.05 : 0.05;
                horizontalOffset += offsetFactor;
                
                // 限制偏移范围
                horizontalOffset = Math.Max(0.0, horizontalOffset);
                
                // 重绘时间轴
                if (midiFile != null)
                {
                    DrawTimeline();
                }
            }
            else
            {
                // 调整垂直偏移量
                double offsetFactor = e.Delta > 0 ? -0.05 : 0.05;
                verticalOffset += offsetFactor;
                
                // 限制偏移范围
                verticalOffset = Math.Max(0.0, verticalOffset);
                
                // 重绘时间轴
                if (midiFile != null)
                {
                    DrawTimeline();
                }
            }
        }

        private void TimelinePanel_MouseHover(object? sender, EventArgs e)
        {
            // 提示信息已通过其他方式显示
        }
    }
}
