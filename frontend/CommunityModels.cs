using System;
using System.Collections.Generic;

namespace SeeMusicApp
{
    public class CommunityScore
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ArtistName { get; set; }
        public string ArrangementTag { get; set; }
        public string Description { get; set; }
        public int PriceCent { get; set; }
        public int DownloadCount { get; set; }
        public int FavoriteCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }
        public string OwnerName { get; set; }
        public string CoverUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        // UI Properties
        public string TitleAbbreviation => Title?.Length > 2 ? Title.Substring(0, 2).ToUpper() : Title?.ToUpper();
        public string Subtitle => $"{ArtistName} · {ArrangementTag}";
        public string PriceString => PriceCent == 0 ? "免费" : $"¥{(PriceCent / 100.0):F2}";
        public string DownloadCountDisplay => DownloadCount >= 1000 ? $"{(DownloadCount / 1000.0):F1}k" : DownloadCount.ToString();
        
        public string FullCoverUrl => string.IsNullOrEmpty(CoverUrl) ? null : 
            (CoverUrl.StartsWith("http") ? CoverUrl : "http://localhost:5000" + CoverUrl);
            
        public bool HasCover => !string.IsNullOrEmpty(CoverUrl);
    }

    public class CommunityComment
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string AvatarUrl { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public string RelativeTime
        {
            get
            {
                var span = DateTime.UtcNow - CreatedAt;
                if (span.TotalDays > 1) return $"{(int)span.TotalDays}天前";
                if (span.TotalHours > 1) return $"{(int)span.TotalHours}小时前";
                if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes}分钟前";
                return "刚刚";
            }
        }
    }

    public class CommunityScoreDetail : CommunityScore
    {
        public List<CommunityComment> RecentComments { get; set; } = new List<CommunityComment>();
        public string CommentCountText => $"社区评论 ({CommentCount})";
    }
}
