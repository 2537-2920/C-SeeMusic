using backend.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace backend.Data
{
    public static class DbSeeder
    {
        public static void Seed(SeeMusicDbContext context)
        {
            if (context.Scores.Any()) return;

            // 确保有一个默认用户
            var admin = context.Users.FirstOrDefault() ?? new User
            {
                Username = "Admin",
                DisplayName = "SeeMusic 官方",
                Email = "admin@seemusic.art",
                PasswordHash = "---", // 仅作演示
                Bio = "致力于分享最高质量的钢琴谱和扒谱作品。"
            };

            if (admin.Id == 0)
            {
                context.Users.Add(admin);
                context.SaveChanges();
            }

            var sampleScores = new List<Score>
            {
                new Score
                {
                    Title = "晴天",
                    ArtistName = "周杰伦",
                    ArrangementTag = "原版钢琴版",
                    Description = "《晴天》是周杰伦创作的一首非常经典的校园情歌。这个版本完美还原了前奏的吉他扫弦转钢琴的意境。",
                    OwnerUserId = admin.Id,
                    PriceCent = 0,
                    DownloadCount = 12500,
                    FavoriteCount = 3400,
                    CommentCount = 42,
                    IsPublic = true,
                    Status = "published",
                    PrimaryCategory = "流行乐",
                    CoverUrl = "https://picsum.photos/seed/sunny/400/600"
                },
                new Score
                {
                    Title = "模特",
                    ArtistName = "李荣浩",
                    ArrangementTag = "爵士风改编",
                    Description = "李荣浩的代表作，加入了一些即兴的爵士和弦，适合进阶练习。",
                    OwnerUserId = admin.Id,
                    PriceCent = 990,
                    DownloadCount = 2100,
                    FavoriteCount = 850,
                    CommentCount = 12,
                    IsPublic = true,
                    Status = "published",
                    PrimaryCategory = "流行乐",
                    CoverUrl = "https://picsum.photos/seed/model/400/600"
                },
                new Score
                {
                    Title = "大鱼",
                    ArtistName = "周深",
                    ArrangementTag = "唯美抒情版",
                    Description = "《大鱼海棠》印象曲。旋律空灵，层次感分明。",
                    OwnerUserId = admin.Id,
                    PriceCent = 500,
                    DownloadCount = 8900,
                    FavoriteCount = 1200,
                    CommentCount = 28,
                    IsPublic = true,
                    Status = "published",
                    PrimaryCategory = "流行乐",
                    CoverUrl = "https://picsum.photos/seed/bigfish/400/600"
                },
                new Score
                {
                    Title = "遇见",
                    ArtistName = "孙燕姿",
                    ArrangementTag = "至简入门版",
                    Description = "非常适合新手练习的一首曲子，左手伴奏简单易上手。",
                    OwnerUserId = admin.Id,
                    PriceCent = 0,
                    DownloadCount = 34000,
                    FavoriteCount = 4500,
                    CommentCount = 56,
                    IsPublic = true,
                    Status = "published",
                    PrimaryCategory = "流行乐",
                    CoverUrl = "https://picsum.photos/seed/meet/400/600"
                }
            };

            context.Scores.AddRange(sampleScores);
            context.SaveChanges();

            // 为“晴天”添加一些评论
            var qingTian = context.Scores.First(s => s.Title == "晴天");
            context.Comments.AddRange(new List<Comment>
            {
                new Comment { ScoreId = qingTian.Id, UserId = admin.Id, Content = "前奏一响，爷青回！", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new Comment { ScoreId = qingTian.Id, UserId = admin.Id, Content = "扒谱非常精准，支持！", CreatedAt = DateTime.UtcNow.AddMinutes(-45) }
            });

            context.SaveChanges();
        }
    }
}
