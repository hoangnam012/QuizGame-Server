using System.Collections.Generic;

namespace GameServer
{
    public class Room
    {
        public string RoomCode { get; set; }
        public string ClassName { get; set; }
        public string Subject { get; set; }

        public string HostIP { get; set; }

        public List<string> Players { get; set; } = new List<string>();

        public Dictionary<string, int> PlayerScores { get; set; } = new Dictionary<string, int>();

        public int CurrentQuestionIndex { get; set; } = 0;
        public string CurrentAnswer { get; set; } = "";   
        public DateTime QuestionStartTime { get; set; }       // Lưu mốc thời gian bắt đầu câu hỏi
        public bool IsAcceptingAnswers { get; set; } = false;

        
    }
}