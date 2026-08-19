using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace eft_where_am_i.Classes
{
    public class QuestRepository
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public QuestRepository()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quest_saves.db");
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS quests (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        map_name TEXT NOT NULL,
                        quest_name TEXT NOT NULL,
                        UNIQUE(map_name, quest_name)
                    )";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                AppLogger.Error("QuestRepository", $"DB 초기화 실패: {ex.Message}");
            }
        }

        public void AddQuest(string mapName, string questName)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR IGNORE INTO quests (map_name, quest_name)
                    VALUES ($mapName, $questName)";
                command.Parameters.AddWithValue("$mapName", mapName);
                command.Parameters.AddWithValue("$questName", questName);
                int rows = command.ExecuteNonQuery();
                AppLogger.Debug("QuestRepository", $"퀘스트 추가 map={mapName} quest={questName} rows={rows}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("QuestRepository", $"퀘스트 추가 실패: {ex.Message}");
            }
        }

        public void RemoveQuest(string mapName, string questName)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    DELETE FROM quests
                    WHERE map_name = $mapName AND quest_name = $questName";
                command.Parameters.AddWithValue("$mapName", mapName);
                command.Parameters.AddWithValue("$questName", questName);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                AppLogger.Error("QuestRepository", $"퀘스트 삭제 실패: {ex.Message}");
            }
        }

        public List<string> GetQuests(string mapName)
        {
            var quests = new List<string>();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT quest_name FROM quests
                    WHERE map_name = $mapName";
                command.Parameters.AddWithValue("$mapName", mapName);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    quests.Add(reader.GetString(0));
                }

                AppLogger.Debug("QuestRepository", $"퀘스트 조회 map={mapName} count={quests.Count}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("QuestRepository", $"퀘스트 조회 실패: {ex.Message}");
            }
            return quests;
        }
    }
}
