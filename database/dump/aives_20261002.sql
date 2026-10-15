-- MySQL dump 10.13  Distrib 8.0.19, for Win64 (x86_64)
--
-- Host: myteam-prn202.c.aivencloud.com    Database: defaultdb
-- ------------------------------------------------------
-- Server version	8.4.8

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
SET @MYSQLDUMP_TEMP_LOG_BIN = @@SESSION.SQL_LOG_BIN;
SET @@SESSION.SQL_LOG_BIN= 0;

--
-- GTID state at the beginning of the backup 
--

SET @@GLOBAL.GTID_PURGED=/*!80000 '+'*/ '98e16368-bb59-11f1-ad39-1a2eafa94605:1-292,
dc75ba5f-ba85-11f1-b799-32efa41dd4b2:1-89';

--
-- Table structure for table `academic_classes`
--

DROP TABLE IF EXISTS `academic_classes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `academic_classes` (
  `class_id` int NOT NULL AUTO_INCREMENT,
  `class_code` varchar(50) NOT NULL,
  `class_name` varchar(200) NOT NULL,
  `course_id` int NOT NULL,
  `lecturer_id` int NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL,
  PRIMARY KEY (`class_id`),
  UNIQUE KEY `uq_academic_classes_code` (`class_code`),
  KEY `ix_academic_classes_course` (`course_id`),
  KEY `ix_academic_classes_lecturer` (`lecturer_id`),
  CONSTRAINT `fk_academic_classes_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_academic_classes_lecturer` FOREIGN KEY (`lecturer_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=12 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `academic_classes`
--

LOCK TABLES `academic_classes` WRITE;
/*!40000 ALTER TABLE `academic_classes` DISABLE KEYS */;
INSERT INTO `academic_classes` VALUES (1,'PRN212-01','Lập trình C# cơ bản - Lớp 01',1,2,1,'2026-09-28 10:00:42',NULL),(2,'PRN222-01','Phát triển ứng dụng web - Lớp 01',2,2,1,'2026-09-28 10:00:42',NULL),(3,'PRN231-01','Lập trình dịch vụ web - Lớp 01',3,2,1,'2026-09-28 10:00:42',NULL),(4,'TEST_EXE201_GV2','Lớp thử EXE201 - Giảng viên 2',7,5,1,'2026-09-28 17:08:32',NULL),(6,'TEST1','TESTING',7,2,1,'2026-09-29 20:49:45',NULL),(9,'TFLOW101-A','Lớp thử C# - nhóm A',11,21,1,'2026-09-29 23:29:00',NULL),(10,'TFLOW202-A','Lớp thử CSDL - nhóm A',12,22,1,'2026-09-29 23:29:00',NULL),(11,'PRO192','PRO192_SPRING26_01',13,2,1,'2026-10-01 17:23:38',NULL);
/*!40000 ALTER TABLE `academic_classes` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `ai_evaluations`
--

DROP TABLE IF EXISTS `ai_evaluations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ai_evaluations` (
  `evaluation_id` int NOT NULL AUTO_INCREMENT,
  `answer_id` int NOT NULL,
  `ai_score` decimal(5,2) NOT NULL,
  `confidence` decimal(5,4) DEFAULT NULL,
  `feedback` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `strengths` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `weaknesses` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `missing_points` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `evaluated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`evaluation_id`),
  UNIQUE KEY `answer_id` (`answer_id`),
  CONSTRAINT `fk_ai_evaluation_answer` FOREIGN KEY (`answer_id`) REFERENCES `answers` (`answer_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `ai_evaluations`
--

LOCK TABLES `ai_evaluations` WRITE;
/*!40000 ALTER TABLE `ai_evaluations` DISABLE KEYS */;
/*!40000 ALTER TABLE `ai_evaluations` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `answers`
--

DROP TABLE IF EXISTS `answers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `answers` (
  `answer_id` int NOT NULL AUTO_INCREMENT,
  `exam_question_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `selected_option_id` int DEFAULT NULL,
  `transcript` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `audio_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `started_at` datetime DEFAULT NULL,
  `finished_at` datetime DEFAULT NULL,
  `duration_seconds` int DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`answer_id`),
  UNIQUE KEY `ux_answers_exam_question` (`exam_question_id`),
  KEY `idx_answers_exam_question` (`exam_question_id`),
  KEY `idx_answers_candidate` (`candidate_id`),
  KEY `fk_answers_selected_option` (`selected_option_id`),
  CONSTRAINT `fk_answers_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`),
  CONSTRAINT `fk_answers_exam_question` FOREIGN KEY (`exam_question_id`) REFERENCES `exam_questions` (`exam_question_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_answers_selected_option` FOREIGN KEY (`selected_option_id`) REFERENCES `question_options` (`option_id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=85 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `answers`
--

LOCK TABLES `answers` WRITE;
/*!40000 ALTER TABLE `answers` DISABLE KEYS */;
INSERT INTO `answers` VALUES (10,7,10,49,NULL,NULL,NULL,'2026-09-29 18:13:52',NULL,'2026-09-29 18:13:52'),(11,8,10,46,NULL,NULL,NULL,'2026-09-29 18:13:52',NULL,'2026-09-29 18:13:52'),(12,9,10,27,NULL,NULL,NULL,'2026-09-29 18:13:52',NULL,'2026-09-29 18:13:52'),(13,10,10,6,NULL,NULL,NULL,'2026-09-29 18:13:52',NULL,'2026-09-29 18:13:52'),(14,11,10,74,NULL,NULL,NULL,'2026-09-29 18:13:52',NULL,'2026-09-29 18:13:52'),(30,36,23,10,NULL,NULL,NULL,'2026-09-29 20:57:57',NULL,'2026-09-29 20:57:57'),(31,37,23,33,NULL,NULL,NULL,'2026-09-29 20:57:57',NULL,'2026-09-29 20:57:57'),(32,38,23,51,NULL,NULL,NULL,'2026-09-29 20:57:57',NULL,'2026-09-29 20:57:57'),(33,39,25,21,NULL,NULL,NULL,'2026-09-29 21:05:24',NULL,'2026-09-29 21:05:24'),(34,40,25,51,NULL,NULL,NULL,'2026-09-29 21:05:24',NULL,'2026-09-29 21:05:24'),(35,41,25,62,NULL,NULL,NULL,'2026-09-29 21:05:24',NULL,'2026-09-29 21:05:24'),(36,42,25,35,NULL,NULL,NULL,'2026-09-29 21:05:24',NULL,'2026-09-29 21:05:24'),(37,43,25,133,NULL,NULL,'2026-09-29 21:05:24','2026-09-29 21:05:39',NULL,'2026-09-29 21:05:39'),(38,44,25,128,NULL,NULL,'2026-09-29 21:05:24','2026-09-29 21:05:39',NULL,'2026-09-29 21:05:39'),(39,45,25,125,NULL,NULL,'2026-09-29 21:05:24','2026-09-29 21:05:39',NULL,'2026-09-29 21:05:39'),(40,46,36,25,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(41,47,36,45,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(42,48,36,37,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(43,49,36,53,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(44,50,36,61,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(45,51,36,33,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(46,52,36,21,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(47,53,36,42,NULL,NULL,NULL,'2026-09-29 22:26:42',NULL,'2026-09-29 22:26:42'),(48,54,36,133,NULL,NULL,'2026-09-29 22:26:42','2026-09-29 22:26:54',NULL,'2026-09-29 22:26:54'),(49,55,36,128,NULL,NULL,'2026-09-29 22:26:42','2026-09-29 22:26:54',NULL,'2026-09-29 22:26:54'),(65,74,58,249,NULL,NULL,'2026-09-28 09:00:00','2026-09-28 09:05:00',NULL,'2026-09-28 09:05:00'),(66,75,58,254,NULL,NULL,'2026-09-28 09:00:00','2026-09-28 09:05:00',NULL,'2026-09-28 09:05:00'),(67,76,58,261,NULL,NULL,'2026-09-28 09:00:00','2026-09-28 09:05:00',NULL,'2026-09-28 09:05:00'),(68,77,58,285,NULL,NULL,'2026-09-28 09:05:00','2026-09-28 09:08:00',NULL,'2026-09-28 09:08:00'),(69,78,58,318,NULL,NULL,'2026-09-28 09:05:00','2026-09-28 09:08:00',NULL,'2026-09-28 09:08:00'),(70,82,55,249,NULL,NULL,NULL,'2026-09-29 23:30:33',NULL,'2026-09-29 23:30:33'),(71,83,55,277,NULL,NULL,NULL,'2026-09-29 23:30:33',NULL,'2026-09-29 23:30:33'),(72,84,55,253,NULL,NULL,NULL,'2026-09-29 23:30:33',NULL,'2026-09-29 23:30:33'),(73,91,55,309,NULL,NULL,'2026-09-29 23:30:33','2026-09-29 23:30:36',NULL,'2026-09-29 23:30:36'),(74,92,55,305,NULL,NULL,'2026-09-29 23:30:33','2026-09-29 23:30:36',NULL,'2026-09-29 23:30:36'),(75,93,73,73,NULL,NULL,NULL,'2026-10-02 01:58:19',NULL,'2026-10-02 01:58:15'),(76,94,73,10,NULL,NULL,NULL,'2026-10-02 01:58:19',NULL,'2026-10-02 01:58:18'),(77,95,73,46,NULL,NULL,NULL,'2026-10-02 01:58:19',NULL,'2026-10-02 01:58:18'),(78,96,73,123,NULL,NULL,'2026-10-02 01:58:19','2026-10-02 01:58:34',NULL,'2026-10-02 01:58:31'),(79,97,73,131,NULL,NULL,'2026-10-02 01:58:19','2026-10-02 01:58:34',NULL,'2026-10-02 01:58:34'),(80,98,74,5,NULL,NULL,NULL,'2026-10-02 02:56:49',NULL,'2026-10-02 02:56:31'),(81,99,74,45,NULL,NULL,NULL,'2026-10-02 02:56:49',NULL,'2026-10-02 02:56:37'),(82,100,74,10,NULL,NULL,NULL,'2026-10-02 02:56:49',NULL,'2026-10-02 02:56:39'),(83,101,74,133,NULL,NULL,'2026-10-02 02:56:49','2026-10-02 02:57:03',NULL,'2026-10-02 02:56:55'),(84,102,74,128,NULL,NULL,'2026-10-02 02:56:49','2026-10-02 02:57:03',NULL,'2026-10-02 02:56:59');
/*!40000 ALTER TABLE `answers` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `class_students`
--

DROP TABLE IF EXISTS `class_students`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `class_students` (
  `class_id` int NOT NULL,
  `student_id` int NOT NULL,
  `joined_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`class_id`,`student_id`),
  KEY `ix_class_students_student` (`student_id`),
  CONSTRAINT `fk_class_students_class` FOREIGN KEY (`class_id`) REFERENCES `academic_classes` (`class_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_class_students_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `class_students`
--

LOCK TABLES `class_students` WRITE;
/*!40000 ALTER TABLE `class_students` DISABLE KEYS */;
INSERT INTO `class_students` VALUES (1,3,'2026-09-28 10:00:49'),(1,4,'2026-09-29 10:57:37'),(1,7,'2026-09-29 19:29:46'),(1,8,'2026-09-29 19:29:50'),(1,13,'2026-09-29 21:52:49'),(2,3,'2026-09-28 10:00:49'),(2,13,'2026-09-29 21:52:32'),(3,3,'2026-09-28 10:00:49'),(4,3,'2026-09-28 17:08:32'),(4,13,'2026-09-29 22:23:05'),(6,3,'2026-10-02 02:00:45'),(9,23,'2026-09-29 23:29:00'),(9,24,'2026-09-29 23:29:00'),(9,25,'2026-09-29 23:29:00'),(10,26,'2026-09-29 23:29:00'),(11,8,'2026-10-01 17:24:14'),(11,9,'2026-10-01 17:24:04'),(11,25,'2026-10-01 17:24:10');
/*!40000 ALTER TABLE `class_students` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `course_materials`
--

DROP TABLE IF EXISTS `course_materials`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `course_materials` (
  `material_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `file_name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_type` enum('PDF','DOCX','PPTX') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_size` bigint DEFAULT NULL,
  `uploaded_by` int NOT NULL,
  `processing_status` enum('Pending','Processing','Completed','Failed') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending',
  `uploaded_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`material_id`),
  KEY `idx_materials_course` (`course_id`),
  KEY `idx_materials_uploader` (`uploaded_by`),
  CONSTRAINT `fk_materials_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_materials_uploader` FOREIGN KEY (`uploaded_by`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `course_materials`
--

LOCK TABLES `course_materials` WRITE;
/*!40000 ALTER TABLE `course_materials` DISABLE KEYS */;
INSERT INTO `course_materials` VALUES (1,7,'bai-giang-01.pdf','/materials/f086d019549043e3a1401e3190e51d23.pdf','PDF',1829,1,'Completed','2026-09-29 17:03:38'),(4,6,'Week 4.pdf','/materials/451f9627acb74e33ba37d262936508bf.pdf','PDF',336570,2,'Completed','2026-09-29 19:24:00'),(5,2,'Presentation SWD392.docx','/materials/290a5a4904744792b702025c435f5fb9.docx','DOCX',20276,2,'Completed','2026-09-29 19:24:21'),(6,2,'Week 4.pdf','/materials/03719851aade41b0a51a27d30d02b73e.pdf','PDF',336570,2,'Completed','2026-09-29 19:24:34'),(7,3,'Week 4.pdf','/materials/0ff4fd91d3e04b57a86f22c31d359b3b.pdf','PDF',336570,2,'Completed','2026-09-29 19:25:02'),(12,11,'tflow-chu-de-1-csharp.pdf','/materials/tflow-chu-de-1.pdf','PDF',1024,21,'Completed','2026-09-29 23:29:00'),(13,11,'tflow-chu-de-2-linq.pdf','/materials/tflow-chu-de-2.pdf','PDF',1024,21,'Completed','2026-09-29 23:29:00'),(14,7,'NOTICE 2.pdf','/materials/9836c8390e0b48d3963e4f8cd2f72f22.pdf','PDF',251717,2,'Completed','2026-10-01 17:05:10');
/*!40000 ALTER TABLE `course_materials` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `courses`
--

DROP TABLE IF EXISTS `courses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `courses` (
  `course_id` int NOT NULL AUTO_INCREMENT,
  `course_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `course_name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `lecturer_id` int NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`course_id`),
  UNIQUE KEY `course_code` (`course_code`),
  KEY `idx_courses_lecturer` (`lecturer_id`),
  CONSTRAINT `fk_courses_lecturer` FOREIGN KEY (`lecturer_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `courses`
--

LOCK TABLES `courses` WRITE;
/*!40000 ALTER TABLE `courses` DISABLE KEYS */;
INSERT INTO `courses` VALUES (1,'PRN212','Lập trình C# cơ bản','Nền tảng C# và .NET.',2,1,'2026-09-28 09:06:17',NULL),(2,'PRN222','Phát triển ứng dụng web','ASP.NET Core MVC, Entity Framework Core.',2,1,'2026-09-28 09:06:17',NULL),(3,'PRN231','Lập trình dịch vụ web','Web API, REST và tích hợp hệ thống.',2,1,'2026-09-28 09:06:17',NULL),(5,'PRF192','Lập trình cơ bản C',NULL,2,0,'2026-09-28 23:32:08','2026-09-28 23:32:37'),(6,'PRF192C','Lập trình cơ bản C trên Coursera',NULL,2,0,'2026-09-28 23:32:44','2026-09-29 22:28:07'),(7,'EXE201','Khởi nghiệp 1','Học cách bắt đầu khởi nghiệp',2,1,'2026-09-29 00:04:09','2026-09-29 20:49:15'),(11,'TFLOW101','Lập trình C# (TEST)','Môn dữ liệu thử cho luồng thi vấn đáp.',21,1,'2026-09-29 23:29:00',NULL),(12,'TFLOW202','Cơ sở dữ liệu (TEST)','Môn của giảng viên B, dùng để thử phân quyền.',22,1,'2026-09-29 23:29:00',NULL),(13,'PRO192','Lập trình cơ bản Java','Lập trình cơ bản Java cho người mới bắt đầu',2,1,'2026-10-01 17:21:54',NULL);
/*!40000 ALTER TABLE `courses` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `exam_candidates`
--

DROP TABLE IF EXISTS `exam_candidates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_candidates` (
  `candidate_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `student_id` int NOT NULL,
  `scheduled_time` datetime DEFAULT NULL,
  `status` enum('Waiting','InProgress','Completed','Absent','Cancelled') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Waiting',
  `started_at` datetime DEFAULT NULL,
  `finished_at` datetime DEFAULT NULL,
  PRIMARY KEY (`candidate_id`),
  UNIQUE KEY `uq_exam_student` (`exam_id`,`student_id`),
  KEY `idx_candidates_exam` (`exam_id`),
  KEY `idx_candidates_student` (`student_id`),
  KEY `idx_candidates_status` (`status`),
  CONSTRAINT `fk_candidates_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_candidates_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=75 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `exam_candidates`
--

LOCK TABLES `exam_candidates` WRITE;
/*!40000 ALTER TABLE `exam_candidates` DISABLE KEYS */;
INSERT INTO `exam_candidates` VALUES (2,2,3,'2026-09-30 07:30:00','Waiting',NULL,NULL),(6,5,3,'2026-09-29 08:00:00','Waiting',NULL,NULL),(7,6,3,'2026-09-30 23:15:00','Waiting',NULL,NULL),(8,7,3,'2026-09-30 21:00:00','Waiting',NULL,NULL),(9,8,3,'2026-09-30 17:14:00','Waiting',NULL,NULL),(10,9,3,'2026-09-29 17:55:00','Completed','2026-09-29 17:54:43','2026-09-29 18:13:52'),(11,10,4,'2026-10-01 08:00:00','Waiting',NULL,NULL),(12,10,3,'2026-10-01 08:20:00','Waiting',NULL,NULL),(13,11,7,'2026-09-30 09:00:00','Waiting',NULL,NULL),(14,11,4,'2026-09-30 09:20:00','Waiting',NULL,NULL),(15,11,8,'2026-09-30 09:40:00','Waiting',NULL,NULL),(16,11,3,'2026-09-30 10:00:00','Waiting',NULL,NULL),(19,13,7,'2026-09-29 20:23:00','Waiting',NULL,NULL),(20,13,4,'2026-09-29 20:28:00','Waiting',NULL,NULL),(21,13,8,'2026-09-29 20:33:00','Waiting',NULL,NULL),(22,13,3,'2026-09-29 20:38:00','Waiting',NULL,NULL),(23,14,7,'2026-09-29 20:57:00','Completed','2026-09-29 20:57:15','2026-09-29 20:57:57'),(24,15,7,'2026-10-09 08:00:00','Waiting',NULL,NULL),(25,16,7,'2026-09-29 21:04:00','Completed','2026-09-29 21:05:10','2026-09-29 21:05:39'),(26,17,7,'2026-10-02 08:00:00','Waiting',NULL,NULL),(27,17,13,'2026-10-02 08:20:00','Waiting',NULL,NULL),(28,17,4,'2026-10-02 08:40:00','Waiting',NULL,NULL),(29,17,8,'2026-10-02 09:00:00','Waiting',NULL,NULL),(30,17,3,'2026-10-02 09:20:00','Waiting',NULL,NULL),(31,18,7,'2026-10-03 08:00:00','Waiting',NULL,NULL),(32,18,13,'2026-10-03 08:20:00','Waiting',NULL,NULL),(33,18,4,'2026-10-03 08:40:00','Waiting',NULL,NULL),(34,18,8,'2026-10-03 09:00:00','Waiting',NULL,NULL),(35,18,3,'2026-10-03 09:20:00','Waiting',NULL,NULL),(36,19,13,'2026-09-29 22:26:00','Completed','2026-09-29 22:26:14','2026-09-29 22:26:54'),(37,19,3,'2026-09-29 22:46:00','Waiting',NULL,NULL),(55,26,23,'2026-09-29 23:28:00','Completed','2026-09-29 23:29:51','2026-09-29 23:30:36'),(56,26,24,'2026-09-29 23:43:00','Cancelled',NULL,NULL),(57,26,25,'2026-09-29 23:58:00','Cancelled',NULL,NULL),(58,27,23,'2026-09-28 09:00:00','Completed','2026-09-28 09:00:00','2026-09-28 09:08:00'),(59,27,24,'2026-09-28 09:10:00','InProgress','2026-09-28 09:10:00',NULL),(60,27,25,'2026-09-28 09:20:00','Waiting',NULL,NULL),(61,28,23,'2026-10-06 10:30:00','Waiting',NULL,NULL),(62,28,24,'2026-10-06 09:20:00','Waiting',NULL,NULL),(64,29,26,'2026-10-01 14:00:00','Waiting',NULL,NULL),(65,30,23,'2026-10-08 08:00:00','Waiting',NULL,NULL),(66,30,24,'2026-10-08 08:10:00','Waiting',NULL,NULL),(67,30,25,'2026-10-08 08:20:00','Waiting',NULL,NULL),(68,28,25,'2026-10-06 09:40:00','Waiting',NULL,NULL),(69,31,7,'2026-10-01 17:30:00','Waiting',NULL,NULL),(70,32,7,'2026-10-03 12:00:00','Waiting',NULL,NULL),(73,34,27,'2026-10-02 01:58:00','Completed','2026-10-02 01:58:09','2026-10-02 01:58:34'),(74,35,3,'2026-10-02 02:56:00','Completed','2026-10-02 02:56:26','2026-10-02 02:57:03');
/*!40000 ALTER TABLE `exam_candidates` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `exam_logs`
--

DROP TABLE IF EXISTS `exam_logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_logs` (
  `log_id` bigint NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int DEFAULT NULL,
  `user_id` int DEFAULT NULL,
  `action` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `timestamp` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`log_id`),
  KEY `idx_logs_exam` (`exam_id`),
  KEY `idx_logs_candidate` (`candidate_id`),
  KEY `idx_logs_user` (`user_id`),
  KEY `idx_logs_timestamp` (`timestamp`),
  CONSTRAINT `fk_logs_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE SET NULL,
  CONSTRAINT `fk_logs_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_logs_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `exam_logs`
--

LOCK TABLES `exam_logs` WRITE;
/*!40000 ALTER TABLE `exam_logs` DISABLE KEYS */;
/*!40000 ALTER TABLE `exam_logs` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `exam_questions`
--

DROP TABLE IF EXISTS `exam_questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_questions` (
  `exam_question_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `question_id` int NOT NULL,
  `parent_exam_question_id` int DEFAULT NULL,
  `order_no` int NOT NULL,
  `asked_at` datetime DEFAULT NULL,
  `is_completed` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`exam_question_id`),
  KEY `idx_exam_questions_exam` (`exam_id`),
  KEY `idx_exam_questions_candidate` (`candidate_id`),
  KEY `idx_exam_questions_question` (`question_id`),
  KEY `idx_candidate_order` (`candidate_id`,`order_no`),
  KEY `fk_exam_questions_parent` (`parent_exam_question_id`),
  CONSTRAINT `fk_exam_questions_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_exam_questions_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_exam_questions_parent` FOREIGN KEY (`parent_exam_question_id`) REFERENCES `exam_questions` (`exam_question_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_exam_questions_question` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`)
) ENGINE=InnoDB AUTO_INCREMENT=103 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `exam_questions`
--

LOCK TABLES `exam_questions` WRITE;
/*!40000 ALTER TABLE `exam_questions` DISABLE KEYS */;
INSERT INTO `exam_questions` VALUES (1,8,9,15,NULL,1,NULL,0),(2,8,9,14,NULL,2,NULL,0),(3,8,9,11,NULL,3,NULL,0),(7,9,10,15,NULL,1,'2026-09-29 18:13:52',1),(8,9,10,14,NULL,2,'2026-09-29 18:13:52',1),(9,9,10,9,NULL,3,'2026-09-29 18:13:52',1),(10,9,10,4,NULL,4,'2026-09-29 18:13:52',1),(11,9,10,21,NULL,5,'2026-09-29 18:13:52',1),(36,14,23,5,NULL,1,'2026-09-29 20:57:57',1),(37,14,23,11,NULL,2,'2026-09-29 20:57:57',1),(38,14,23,15,NULL,3,'2026-09-29 20:57:57',1),(39,16,25,8,NULL,1,'2026-09-29 21:05:24',1),(40,16,25,15,NULL,2,'2026-09-29 21:05:24',1),(41,16,25,18,NULL,3,'2026-09-29 21:05:24',1),(42,16,25,11,NULL,4,'2026-09-29 21:05:24',1),(43,16,25,7,39,5,'2026-09-29 21:05:24',1),(44,16,25,6,40,6,'2026-09-29 21:05:24',1),(45,16,25,10,41,7,'2026-09-29 21:05:24',1),(46,19,36,9,NULL,1,'2026-09-29 22:26:42',1),(47,19,36,14,NULL,2,'2026-09-29 22:26:42',1),(48,19,36,12,NULL,3,'2026-09-29 22:26:42',1),(49,19,36,16,NULL,4,'2026-09-29 22:26:42',1),(50,19,36,18,NULL,5,'2026-09-29 22:26:42',1),(51,19,36,11,NULL,6,'2026-09-29 22:26:42',1),(52,19,36,8,NULL,7,'2026-09-29 22:26:42',1),(53,19,36,13,NULL,8,'2026-09-29 22:26:42',1),(54,19,36,7,46,9,'2026-09-29 22:26:42',1),(55,19,36,6,47,10,'2026-09-29 22:26:42',1),(74,27,58,61,NULL,1,'2026-09-28 09:05:00',1),(75,27,58,62,NULL,2,'2026-09-28 09:05:00',1),(76,27,58,64,NULL,3,'2026-09-28 09:05:00',1),(77,27,58,70,75,4,'2026-09-28 09:05:00',1),(78,27,58,78,76,5,'2026-09-28 09:05:00',1),(79,27,59,58,NULL,1,NULL,0),(80,27,59,65,NULL,2,NULL,0),(81,27,59,69,NULL,3,NULL,0),(82,26,55,61,NULL,1,'2026-09-29 23:30:33',1),(83,26,55,68,NULL,2,'2026-09-29 23:30:33',1),(84,26,55,62,NULL,3,'2026-09-29 23:30:33',1),(85,26,56,58,NULL,1,NULL,0),(86,26,56,63,NULL,2,NULL,0),(87,26,56,64,NULL,3,NULL,0),(88,26,57,60,NULL,1,NULL,0),(89,26,57,69,NULL,2,NULL,0),(90,26,57,67,NULL,3,NULL,0),(91,26,55,76,83,4,'2026-09-29 23:30:33',1),(92,26,55,75,82,5,'2026-09-29 23:30:33',1),(93,34,73,21,NULL,1,'2026-10-02 01:58:19',1),(94,34,73,5,NULL,2,'2026-10-02 01:58:19',1),(95,34,73,14,NULL,3,'2026-10-02 01:58:19',1),(96,34,73,10,93,4,'2026-10-02 01:58:19',1),(97,34,73,7,94,5,'2026-10-02 01:58:19',1),(98,35,74,4,NULL,1,'2026-10-02 02:56:49',1),(99,35,74,14,NULL,2,'2026-10-02 02:56:49',1),(100,35,74,5,NULL,3,'2026-10-02 02:56:49',1),(101,35,74,7,98,4,'2026-10-02 02:56:49',1),(102,35,74,6,99,5,'2026-10-02 02:56:49',1);
/*!40000 ALTER TABLE `exam_questions` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `exam_recordings`
--

DROP TABLE IF EXISTS `exam_recordings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_recordings` (
  `recording_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `file_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_type` enum('Audio','Video') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `duration_seconds` int DEFAULT NULL,
  `started_at` datetime DEFAULT NULL,
  `ended_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`recording_id`),
  KEY `idx_recording_exam` (`exam_id`),
  KEY `idx_recording_candidate` (`candidate_id`),
  CONSTRAINT `fk_recording_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_recording_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `exam_recordings`
--

LOCK TABLES `exam_recordings` WRITE;
/*!40000 ALTER TABLE `exam_recordings` DISABLE KEYS */;
/*!40000 ALTER TABLE `exam_recordings` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `exam_sessions`
--

DROP TABLE IF EXISTS `exam_sessions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_sessions` (
  `exam_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `lecturer_id` int NOT NULL,
  `exam_name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `start_time` datetime NOT NULL,
  `end_time` datetime NOT NULL,
  `time_per_student` int NOT NULL,
  `main_question_count` int NOT NULL DEFAULT '3',
  `max_follow_up_count` int NOT NULL DEFAULT '2',
  `status` enum('Draft','Scheduled','InProgress','Completed','Cancelled') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Draft',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `question_scope_json` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`exam_id`),
  KEY `idx_exam_course` (`course_id`),
  KEY `idx_exam_lecturer` (`lecturer_id`),
  KEY `idx_exam_status` (`status`),
  CONSTRAINT `fk_exam_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_exam_lecturer` FOREIGN KEY (`lecturer_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `exam_sessions`
--

LOCK TABLES `exam_sessions` WRITE;
/*!40000 ALTER TABLE `exam_sessions` DISABLE KEYS */;
INSERT INTO `exam_sessions` VALUES (2,1,2,'Thi vấn đáp môn PRN212',NULL,'2026-09-30 07:30:00','2026-09-30 09:00:00',90,5,2,'Scheduled','2026-09-28 17:02:05',NULL,NULL),(5,1,2,'PRF192c - Review Final ASM',NULL,'2026-09-29 08:00:00','2026-09-29 08:20:00',20,3,2,'Cancelled','2026-09-28 23:34:24','2026-09-28 23:39:30',NULL),(6,2,2,'Review Lab2',NULL,'2026-09-30 23:15:00','2026-09-30 23:40:00',25,5,2,'Scheduled','2026-09-28 23:36:39','2026-09-28 23:37:15',NULL),(7,7,5,'Review CP1',NULL,'2026-09-30 21:00:00','2026-09-30 21:20:00',20,3,2,'Scheduled','2026-09-29 00:09:49','2026-09-29 00:10:05',NULL),(8,7,5,'Review CP3',NULL,'2026-09-30 17:14:00','2026-09-30 17:34:00',20,3,2,'Scheduled','2026-09-29 00:16:47','2026-09-29 17:13:54',NULL),(9,7,5,'Thi vấn đáp môn EXE201',NULL,'2026-09-29 17:55:00','2026-09-29 18:15:00',20,5,2,'Completed','2026-09-29 17:54:13','2026-10-01 19:15:48',NULL),(10,1,2,'Checkpoint 4',NULL,'2026-10-01 08:00:00','2026-10-01 08:40:00',20,3,2,'Scheduled','2026-09-29 18:43:04',NULL,NULL),(11,1,2,'Thi vấn đáp môn PRN212',NULL,'2026-09-30 09:00:00','2026-09-30 10:20:00',20,1,2,'Scheduled','2026-09-29 19:31:57',NULL,NULL),(13,1,2,'Test đợt 2',NULL,'2026-09-29 20:23:00','2026-09-29 20:43:00',5,3,2,'Scheduled','2026-09-29 20:22:11','2026-09-29 20:23:26',NULL),(14,7,2,'Test3',NULL,'2026-09-29 20:57:00','2026-09-29 21:02:00',5,3,2,'Completed','2026-09-29 20:56:12','2026-10-01 19:15:48',NULL),(15,7,2,'Thi vấn đáp môn EXE201 mã 29092026',NULL,'2026-10-09 08:00:00','2026-10-09 08:20:00',20,3,2,'Scheduled','2026-09-29 21:00:34',NULL,NULL),(16,7,2,'Test đợt 4',NULL,'2026-09-29 21:04:00','2026-09-29 21:08:00',4,4,3,'Completed','2026-09-29 21:03:47','2026-10-01 19:15:48',NULL),(17,1,2,'prn232',NULL,'2026-10-02 08:00:00','2026-10-02 09:40:00',20,3,2,'Scheduled','2026-09-29 21:57:56',NULL,NULL),(18,1,2,'Review Lab4',NULL,'2026-10-03 08:00:00','2026-10-03 09:40:00',20,3,2,'Scheduled','2026-09-29 22:01:36',NULL,NULL),(19,7,5,'exeTest',NULL,'2026-09-29 22:26:00','2026-09-29 23:06:00',20,8,2,'Scheduled','2026-09-29 22:25:17',NULL,NULL),(26,11,21,'[TFLOW-1] Thi ngay','SV1 vào thi được ngay; SV2, SV3 đợi tới ca của mình.','2026-09-29 23:28:00','2026-09-30 00:13:00',15,3,2,'Cancelled','2026-09-29 23:29:00','2026-09-29 23:30:39',NULL),(27,11,21,'[TFLOW-2] Đã thi hôm qua','SV1 đã thi xong, SV2 bỏ ngang, SV3 không đến. Dùng để thử Chốt ca và Kết quả.','2026-09-28 09:00:00','2026-09-28 09:30:00',10,3,2,'InProgress','2026-09-29 23:29:00',NULL,NULL),(28,11,21,'[TFLOW-3] Tuần sau (đã đổi tên)','Mọi SV còn Chờ thi: sửa, đổi giờ, thêm/xoá SV, xoá phiên đều được.','2026-10-06 09:20:00','2026-10-06 10:50:00',20,4,2,'Scheduled','2026-09-29 23:29:00','2026-09-29 23:31:27',NULL),(29,12,22,'[TFLOW-4] Phiên của GV B','Giảng viên A mở phiên này phải bị chặn.','2026-10-01 14:00:00','2026-10-01 14:15:00',15,3,0,'Scheduled','2026-09-29 23:29:00',NULL,NULL),(30,11,21,'[TFLOW-6] Vừa đủ 12 câu',NULL,'2026-10-08 08:00:00','2026-10-08 08:30:00',10,4,2,'Scheduled','2026-09-29 23:30:55',NULL,NULL),(31,7,2,'Quiz 3',NULL,'2026-10-01 17:30:00','2026-10-01 17:50:00',20,3,2,'Scheduled','2026-10-01 17:29:08',NULL,NULL),(32,7,2,'ReQuiz',NULL,'2026-10-03 12:00:00','2026-10-03 12:20:00',20,3,2,'Scheduled','2026-10-02 01:40:44','2026-10-02 01:41:03',NULL),(34,7,2,'ReQuiz 4',NULL,'2026-10-02 01:58:00','2026-10-02 02:18:00',20,3,2,'Completed','2026-10-02 01:57:37','2026-10-01 19:15:48',NULL),(35,7,2,'ReQuiz10',NULL,'2026-10-02 02:56:00','2026-10-02 03:16:00',20,3,2,'Completed','2026-10-02 02:55:08','2026-10-02 02:57:03',NULL);
/*!40000 ALTER TABLE `exam_sessions` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `final_results`
--

DROP TABLE IF EXISTS `final_results`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `final_results` (
  `result_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `ai_score` decimal(5,2) DEFAULT NULL,
  `lecturer_score` decimal(5,2) DEFAULT NULL,
  `final_score` decimal(5,2) DEFAULT NULL,
  `lecturer_comment` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `finalized_by` int DEFAULT NULL,
  `finalized_at` datetime DEFAULT NULL,
  PRIMARY KEY (`result_id`),
  UNIQUE KEY `uq_result_exam_candidate` (`exam_id`,`candidate_id`),
  KEY `idx_results_exam` (`exam_id`),
  KEY `idx_results_candidate` (`candidate_id`),
  KEY `idx_results_finalized_by` (`finalized_by`),
  CONSTRAINT `fk_result_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_result_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_result_lecturer` FOREIGN KEY (`finalized_by`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `final_results`
--

LOCK TABLES `final_results` WRITE;
/*!40000 ALTER TABLE `final_results` DISABLE KEYS */;
/*!40000 ALTER TABLE `final_results` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `follow_up_questions`
--

DROP TABLE IF EXISTS `follow_up_questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `follow_up_questions` (
  `follow_up_id` int NOT NULL AUTO_INCREMENT,
  `answer_id` int NOT NULL,
  `question_text` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `answer_transcript` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `audio_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `reason` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `order_no` int NOT NULL DEFAULT '1',
  `asked_at` datetime DEFAULT NULL,
  `answered_at` datetime DEFAULT NULL,
  PRIMARY KEY (`follow_up_id`),
  KEY `idx_followup_answer` (`answer_id`),
  CONSTRAINT `fk_followup_answer` FOREIGN KEY (`answer_id`) REFERENCES `answers` (`answer_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `follow_up_questions`
--

LOCK TABLES `follow_up_questions` WRITE;
/*!40000 ALTER TABLE `follow_up_questions` DISABLE KEYS */;
/*!40000 ALTER TABLE `follow_up_questions` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `question_options`
--

DROP TABLE IF EXISTS `question_options`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `question_options` (
  `option_id` int NOT NULL AUTO_INCREMENT,
  `question_id` int NOT NULL,
  `option_text` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_correct` tinyint(1) NOT NULL DEFAULT '0',
  `display_order` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`option_id`),
  KEY `ix_question_options_question` (`question_id`),
  CONSTRAINT `fk_question_options_question` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=333 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `question_options`
--

LOCK TABLES `question_options` WRITE;
/*!40000 ALTER TABLE `question_options` DISABLE KEYS */;
INSERT INTO `question_options` VALUES (5,4,'Là một phần cứng chuyên dụng để lưu trữ dữ liệu tạm thời trong máy tính.',0,1),(6,4,'Là cách tổ chức, quản lý và lưu trữ dữ liệu trong máy tính để có thể truy cập và sửa đổi hiệu quả',1,2),(7,4,'Là ngôn ngữ lập trình dùng để viết hệ điều hành.',0,3),(8,4,'Là một thuật toán mã hóa dữ liệu nhằm bảo mật thông tin trên mạng.',0,4),(9,5,'int',1,1),(10,5,'long',0,2),(11,5,'short',0,3),(12,5,'byte',0,4),(21,8,'this',0,1),(22,8,'base',0,2),(23,8,'new',1,3),(24,8,'var',0,4),(25,9,'try',0,1),(26,9,'catch',0,2),(27,9,'finally',1,3),(28,9,'throw',0,4),(33,11,'class là kiểu tham chiếu, struct là kiểu tham trị',1,1),(34,11,'struct không có phương thức',0,2),(35,11,'class không kế thừa được',0,3),(36,11,'struct luôn nhanh hơn class',0,4),(37,12,'Interface có hàm khởi tạo',0,1),(38,12,'Interface lưu được trạng thái',0,2),(39,12,'Một lớp cài đặt được nhiều interface nhưng chỉ kế thừa một lớp',1,3),(40,12,'Abstract class không có phương thức trừu tượng',0,4),(41,13,'Đối tượng không bao giờ null',0,1),(42,13,'Dispose được gọi khi ra khỏi phạm vi',1,2),(43,13,'Biến trở thành static',0,3),(44,13,'Bộ nhớ được giải phóng ngay',0,4),(45,14,'So sánh hai giá trị',0,1),(46,14,'Trả về vế phải khi vế trái null',1,2),(47,14,'Ép kiểu an toàn',0,3),(48,14,'Kiểm tra kiểu',0,4),(49,15,'Select',0,1),(50,15,'Where',1,2),(51,15,'OrderBy',0,3),(52,15,'GroupBy',0,4),(53,16,'Ngay khi khai báo',0,1),(54,16,'Khi duyệt kết quả, ví dụ gọi ToList()',1,2),(55,16,'Khi chương trình kết thúc',0,3),(56,16,'Khi gọi Dispose',0,4),(57,17,'float',0,1),(58,17,'double',0,2),(59,17,'decimal',1,3),(60,17,'int',0,4),(61,18,'Bất biến, mỗi lần sửa tạo chuỗi mới',1,1),(62,18,'Thay đổi tại chỗ',0,2),(63,18,'Là kiểu tham trị',0,3),(64,18,'Luôn có độ dài cố định',0,4),(65,19,'Vì string không nối được',0,1),(66,19,'Tránh tạo nhiều chuỗi trung gian',1,2),(67,19,'Vì StringBuilder là kiểu tham trị',0,3),(68,19,'Vì StringBuilder chạy bất đồng bộ',0,4),(73,21,'Vì IEnumerable nhanh hơn',0,1),(74,21,'Vì List không duyệt được',0,2),(75,21,'Chỉ lộ ra khả năng duyệt, không cho sửa tập hợp',1,3),(76,21,'Vì IEnumerable luôn nằm trên stack',0,4),(83,24,'Web, ứng dụng, moblie',1,1),(84,24,'Ngân hàng',0,2),(85,24,'Công trường',0,3),(86,24,'Lớp học',0,4),(115,22,'Khi cần kế thừa nhiều tầng',0,1),(116,22,'Khi dữ liệu nhỏ, bất biến và tạo ra rất nhiều',1,2),(117,22,'Khi cần truyền theo tham chiếu',0,3),(118,22,'Khi cần hàm huỷ',0,4),(119,20,'Chạy nhanh hơn trên mọi trường hợp',0,1),(120,20,'Không chặn luồng khi chờ tác vụ I/O',1,2),(121,20,'Tự động bắt ngoại lệ',0,3),(122,20,'Giảm bộ nhớ sử dụng',0,4),(123,10,'Thuộc về lớp',1,1),(124,10,'Thuộc về từng đối tượng',0,2),(125,10,'Thuộc về namespace',0,3),(126,10,'Thuộc về assembly',0,4),(127,6,'readonly',0,1),(128,6,'const',1,2),(129,6,'static',0,3),(130,6,'sealed',0,4),(131,7,'Trên heap',0,1),(132,7,'Trên stack',1,2),(133,7,'Trong vùng static',0,3),(134,7,'Trong registry',0,4),(237,58,'int',1,1),(238,58,'long',0,2),(239,58,'short',0,3),(240,58,'byte',0,4),(241,59,'this',0,1),(242,59,'base',1,2),(243,59,'super',0,3),(244,59,'parent',0,4),(245,60,'Start',0,1),(246,60,'Run',0,2),(247,60,'Main',1,3),(248,60,'Init',0,4),(249,61,'class là kiểu tham chiếu, struct là kiểu giá trị',1,1),(250,61,'struct không được có phương thức',0,2),(251,61,'class không có hàm khởi tạo',0,3),(252,61,'struct luôn được cấp phát trên heap',0,4),(253,62,'virtual ở lớp cha và override ở lớp con',1,1),(254,62,'static ở lớp cha và new ở lớp con',0,2),(255,62,'sealed ở lớp cha và override ở lớp con',0,3),(256,62,'abstract ở lớp con',0,4),(257,63,'Biến int trở thành tham chiếu tới stack',0,1),(258,63,'Không có cấp phát bộ nhớ nào',0,2),(259,63,'Giá trị được sao chép vào một object trên heap',1,3),(260,63,'int bị chuyển thành string',0,4),(261,64,'Where',1,1),(262,64,'Select',0,2),(263,64,'OrderBy',0,3),(264,64,'GroupBy',0,4),(265,65,'List<T>',0,1),(266,65,'Queue<T>',0,2),(267,65,'Stack<T>',0,3),(268,65,'Dictionary<TKey, TValue>',1,4),(269,66,'Where',0,1),(270,66,'Select',1,2),(271,66,'Any',0,3),(272,66,'Count',0,4),(273,67,'Ngay khi khai báo truy vấn',0,1),(274,67,'Khi truy vấn được duyệt (deferred execution)',1,2),(275,67,'Lúc biên dịch',0,3),(276,67,'Khi bộ thu gom rác chạy',0,4),(277,68,'Cả hai đều trả về null',0,1),(278,68,'Cả hai đều ném ngoại lệ',0,2),(279,68,'First() ném ngoại lệ, FirstOrDefault() trả về giá trị mặc định',1,3),(280,68,'First() trả về giá trị mặc định, FirstOrDefault() ném ngoại lệ',0,4),(281,69,'Điều kiện được dịch sang SQL và lọc ở database',1,1),(282,69,'IQueryable chạy trong bộ nhớ nhanh hơn',0,2),(283,69,'IEnumerable không hỗ trợ Where',0,3),(284,69,'IQueryable không cần kết nối database',0,4),(285,70,'true và false',1,1),(286,70,'0, 1 và 2',0,2),(287,70,'yes và no',0,3),(288,70,'on và off',0,4),(289,71,'=',0,1),(290,71,'==',1,2),(291,71,'===',0,3),(292,71,':=',0,4),(293,72,'Có, như lớp thường',0,1),(294,72,'Không, chỉ lớp con cụ thể mới tạo được',1,2),(295,72,'Chỉ khi có hàm khởi tạo public',0,3),(296,72,'Chỉ trong cùng namespace',0,4),(297,73,'Lưu trữ dữ liệu dùng chung',0,1),(298,73,'Thay thế cho struct',0,2),(299,73,'Định nghĩa hợp đồng các thành viên mà lớp phải cài đặt',1,3),(300,73,'Tạo luồng xử lý mới',0,4),(301,74,'Giá trị từng trường',1,1),(302,74,'Địa chỉ bộ nhớ',0,2),(303,74,'Luôn trả về false',0,3),(304,74,'Chỉ so sánh tên kiểu',0,4),(305,75,'Ngăn tạo thể hiện',0,1),(306,75,'Biến lớp thành static',0,2),(307,75,'Ẩn lớp khỏi assembly khác',0,3),(308,75,'Ngăn lớp khác kế thừa',1,4),(309,76,'Sum',0,1),(310,76,'Count',1,2),(311,76,'Max',0,3),(312,76,'Length',0,4),(313,77,'Add',1,1),(314,77,'Push',0,2),(315,77,'Enqueue',0,3),(316,77,'Put',0,4),(317,78,'true nếu có ít nhất một phần tử thoả điều kiện',1,1),(318,78,'Phần tử đầu tiên thoả điều kiện',0,2),(319,78,'Số phần tử thoả điều kiện',0,3),(320,78,'Một danh sách rỗng',0,4),(321,79,'Dictionary<TKey, TElement>',0,1),(322,79,'List<TKey>',0,2),(323,79,'Tập các IGrouping<TKey, TElement>',1,3),(324,79,'Một phần tử duy nhất',0,4),(325,80,'Không có ảnh hưởng gì',0,1),(326,80,'Truy vấn chạy ngay, phần sau xử lý trong bộ nhớ',1,2),(327,80,'Truy vấn chạy trên server nhanh hơn',0,3),(328,80,'Database tự thêm index',0,4),(329,81,'Do cache của EF bị tắt',0,1),(330,81,'Do bộ thu gom rác',0,2),(331,81,'Không bao giờ xảy ra',0,3),(332,81,'Mỗi lần duyệt thực thi lại truy vấn (deferred execution)',1,4);
/*!40000 ALTER TABLE `question_options` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `questions`
--

DROP TABLE IF EXISTS `questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `questions` (
  `question_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `source_material_id` int DEFAULT NULL,
  `created_by` int NOT NULL,
  `question_text` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `expected_answer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `bloom_level` enum('Remember','Understand','Apply','Analyze') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `difficulty` enum('Easy','Medium','Hard') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `question_type` enum('Main','FollowUp') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Main',
  `status` enum('Draft','PendingReview','Approved','Rejected','Archived') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Draft',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`question_id`),
  KEY `idx_questions_course` (`course_id`),
  KEY `idx_questions_material` (`source_material_id`),
  KEY `idx_questions_creator` (`created_by`),
  KEY `idx_questions_status` (`status`),
  KEY `idx_questions_bloom` (`bloom_level`),
  KEY `idx_questions_bank` (`course_id`,`question_type`,`status`),
  CONSTRAINT `fk_questions_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_questions_creator` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`),
  CONSTRAINT `fk_questions_material` FOREIGN KEY (`source_material_id`) REFERENCES `course_materials` (`material_id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=82 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `questions`
--

LOCK TABLES `questions` WRITE;
/*!40000 ALTER TABLE `questions` DISABLE KEYS */;
INSERT INTO `questions` VALUES (4,7,1,1,'Cấu trúc dữ liệu (Data Structure) là gì?',NULL,'Understand','Medium','Main','Approved','2026-09-29 17:11:03',NULL),(5,7,1,1,'Trong C#, kiểu nào lưu số nguyên 32 bit có dấu?','int','Remember','Easy','Main','Approved','2026-09-29 17:11:45',NULL),(6,7,1,1,'Từ khoá nào khai báo một hằng số biết giá trị ngay lúc biên dịch?','const','Remember','Easy','FollowUp','Approved','2026-09-29 17:11:46','2026-09-29 21:02:49'),(7,7,1,1,'Kiểu dữ liệu tham trị được cấp phát ở đâu?','Trên stack','Remember','Easy','FollowUp','Approved','2026-09-29 17:11:46','2026-09-29 21:03:05'),(8,7,1,1,'Từ khoá nào tạo một thể hiện mới của lớp?','new','Remember','Easy','Main','Approved','2026-09-29 17:11:47',NULL),(9,7,1,1,'Khối nào luôn chạy dù có ngoại lệ hay không?','finally','Understand','Easy','Main','Approved','2026-09-29 17:11:48',NULL),(10,7,1,1,'Thành viên static thuộc về đâu?','Thuộc về lớp','Understand','Medium','FollowUp','Approved','2026-09-29 17:11:49','2026-09-29 21:02:23'),(11,7,1,1,'Sự khác nhau chính giữa class và struct trong C# là gì?','class là kiểu tham chiếu, struct là kiểu tham trị','Understand','Medium','Main','Approved','2026-09-29 17:11:49',NULL),(12,7,1,1,'Interface khác abstract class ở điểm nào?','Một lớp cài đặt được nhiều interface nhưng chỉ kế thừa một lớp','Understand','Medium','Main','Approved','2026-09-29 17:11:50',NULL),(13,7,1,1,'Câu lệnh using trong thân phương thức bảo đảm điều gì?','Dispose được gọi khi ra khỏi phạm vi','Understand','Medium','Main','Approved','2026-09-29 17:11:51',NULL),(14,7,1,1,'Toán tử ?? trong C# làm gì?','Trả về vế phải khi vế trái null','Apply','Medium','Main','Approved','2026-09-29 17:11:52',NULL),(15,7,1,1,'Phương thức LINQ nào dùng để lọc phần tử theo điều kiện?','Where','Apply','Medium','Main','Approved','2026-09-29 17:11:52',NULL),(16,7,1,1,'Truy vấn LINQ được thực thi khi nào?','Khi duyệt kết quả, ví dụ gọi ToList()','Understand','Medium','Main','Approved','2026-09-29 17:11:53',NULL),(17,7,1,1,'Kiểu nào nên dùng cho số tiền để tránh sai số làm tròn?','decimal','Apply','Medium','Main','Approved','2026-09-29 17:11:54',NULL),(18,7,1,1,'string trong C# có đặc điểm gì?','Bất biến, mỗi lần sửa tạo chuỗi mới','Understand','Medium','Main','Approved','2026-09-29 17:11:55',NULL),(19,7,1,1,'Vì sao nên dùng StringBuilder khi nối chuỗi trong vòng lặp?','Tránh tạo nhiều chuỗi trung gian','Analyze','Hard','Main','Approved','2026-09-29 17:11:56',NULL),(20,7,1,1,'async/await giúp giải quyết vấn đề gì?','Không chặn luồng khi chờ tác vụ I/O','Analyze','Hard','FollowUp','Approved','2026-09-29 17:11:56','2026-09-29 21:01:52'),(21,7,1,1,'Vì sao IEnumerable phù hợp hơn List khi trả dữ liệu ra ngoài?','Chỉ lộ ra khả năng duyệt, không cho sửa tập hợp','Analyze','Hard','Main','Approved','2026-09-29 17:11:57',NULL),(22,7,1,1,'Khi nào nên chọn struct thay cho class?','Khi dữ liệu nhỏ, bất biến và tạo ra rất nhiều','Analyze','Hard','FollowUp','Approved','2026-09-29 17:11:58','2026-09-29 21:01:37'),(24,6,4,2,'PRN222 có thể code được cái nào?',NULL,'Understand','Medium','Main','Approved','2026-09-29 19:27:14',NULL),(58,11,12,21,'Trong C#, kiểu nào lưu số nguyên 32 bit có dấu?','int (System.Int32).','Remember','Easy','Main','Approved','2026-09-29 23:29:00',NULL),(59,11,12,21,'Từ khoá nào dùng để gọi hàm khởi tạo của lớp cha?','base(...) đặt sau khai báo hàm khởi tạo.','Remember','Easy','Main','Approved','2026-09-29 23:29:00',NULL),(60,11,12,21,'Phương thức nào là điểm bắt đầu của một chương trình console C#?','static void Main (hoặc top-level statements).','Remember','Easy','Main','Approved','2026-09-29 23:29:00',NULL),(61,11,12,21,'Khác biệt chính giữa class và struct trong C# là gì?','class là kiểu tham chiếu, struct là kiểu giá trị (sao chép khi gán).','Understand','Medium','Main','Approved','2026-09-29 23:29:00',NULL),(62,11,12,21,'Cần những từ khoá nào để lớp con ghi đè phương thức của lớp cha?','virtual (hoặc abstract) ở lớp cha, override ở lớp con.','Understand','Medium','Main','Approved','2026-09-29 23:29:00',NULL),(63,11,12,21,'Khi boxing một giá trị int, điều gì xảy ra?','Giá trị được sao chép vào một object mới cấp phát trên heap.','Analyze','Hard','Main','Approved','2026-09-29 23:29:00',NULL),(64,11,13,21,'Phương thức LINQ nào lọc phần tử theo điều kiện?','Where.','Remember','Easy','Main','Approved','2026-09-29 23:29:00',NULL),(65,11,13,21,'Collection nào lưu các cặp khoá - giá trị?','Dictionary<TKey, TValue>.','Remember','Easy','Main','Approved','2026-09-29 23:29:00',NULL),(66,11,13,21,'Phương thức LINQ nào biến đổi mỗi phần tử sang một dạng mới?','Select.','Remember','Easy','Main','Approved','2026-09-29 23:29:00',NULL),(67,11,13,21,'Truy vấn LINQ trên IEnumerable được thực thi khi nào?','Khi được duyệt (deferred execution), không phải lúc khai báo.','Understand','Medium','Main','Approved','2026-09-29 23:29:00',NULL),(68,11,13,21,'Khi chuỗi rỗng, First() khác FirstOrDefault() ở điểm nào?','First() ném InvalidOperationException, FirstOrDefault() trả về default.','Understand','Medium','Main','Approved','2026-09-29 23:29:00',NULL),(69,11,13,21,'Vì sao nên giữ IQueryable thay vì IEnumerable khi truy vấn EF Core?','Điều kiện được dịch sang SQL, lọc ngay tại database.','Analyze','Hard','Main','Approved','2026-09-29 23:29:00',NULL),(70,11,12,21,'[Đào sâu] Kiểu bool nhận những giá trị nào?','true và false.','Remember','Easy','FollowUp','Approved','2026-09-29 23:29:00',NULL),(71,11,12,21,'[Đào sâu] Toán tử nào so sánh bằng trong C#?','==','Remember','Easy','FollowUp','Approved','2026-09-29 23:29:00',NULL),(72,11,12,21,'[Đào sâu] Có thể dùng new để tạo thể hiện trực tiếp của lớp abstract không?','Không, chỉ tạo được từ lớp con cụ thể.','Understand','Medium','FollowUp','Approved','2026-09-29 23:29:00',NULL),(73,11,12,21,'[Đào sâu] interface trong C# dùng để làm gì?','Định nghĩa hợp đồng các thành viên mà lớp cài đặt phải có.','Understand','Medium','FollowUp','Approved','2026-09-29 23:29:00',NULL),(74,11,12,21,'[Đào sâu] Equals mặc định của struct so sánh điều gì?','So sánh giá trị từng trường.','Analyze','Hard','FollowUp','Approved','2026-09-29 23:29:00',NULL),(75,11,12,21,'[Đào sâu] Đặt sealed lên một lớp có tác dụng gì?','Ngăn không cho lớp khác kế thừa.','Analyze','Hard','FollowUp','Approved','2026-09-29 23:29:00',NULL),(76,11,13,21,'[Đào sâu] Phương thức LINQ nào đếm số phần tử?','Count.','Remember','Easy','FollowUp','Approved','2026-09-29 23:29:00',NULL),(77,11,13,21,'[Đào sâu] Thêm một phần tử vào List<T> bằng phương thức nào?','Add.','Remember','Easy','FollowUp','Approved','2026-09-29 23:29:00',NULL),(78,11,13,21,'[Đào sâu] Any(điều kiện) trả về gì?','true nếu có ít nhất một phần tử thoả điều kiện.','Understand','Medium','FollowUp','Approved','2026-09-29 23:29:00',NULL),(79,11,13,21,'[Đào sâu] GroupBy trả về kiểu gì?','Một chuỗi IGrouping<TKey, TElement>.','Understand','Medium','FollowUp','Approved','2026-09-29 23:29:00',NULL),(80,11,13,21,'[Đào sâu] Gọi ToList() ở giữa truy vấn EF Core gây ra điều gì?','Truy vấn chạy ngay; phần phía sau xử lý trong bộ nhớ.','Analyze','Hard','FollowUp','Approved','2026-09-29 23:29:00',NULL),(81,11,13,21,'[Đào sâu] Vì sao duyệt cùng một IQueryable hai lần có thể gọi database hai lần?','Deferred execution: mỗi lần duyệt thực thi lại truy vấn.','Analyze','Hard','FollowUp','Approved','2026-09-29 23:29:00',NULL);
/*!40000 ALTER TABLE `questions` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `roles`
--

DROP TABLE IF EXISTS `roles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `roles` (
  `role_id` int NOT NULL AUTO_INCREMENT,
  `role_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`role_id`),
  UNIQUE KEY `role_name` (`role_name`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `roles`
--

LOCK TABLES `roles` WRITE;
/*!40000 ALTER TABLE `roles` DISABLE KEYS */;
INSERT INTO `roles` VALUES (1,'Admin','System administrator'),(2,'Lecturer','Lecturer / Examiner'),(3,'Student','Student / Exam candidate');
/*!40000 ALTER TABLE `roles` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `rubric_criteria`
--

DROP TABLE IF EXISTS `rubric_criteria`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rubric_criteria` (
  `criteria_id` int NOT NULL AUTO_INCREMENT,
  `rubric_id` int NOT NULL,
  `criteria_name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `max_score` decimal(5,2) NOT NULL,
  `display_order` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`criteria_id`),
  KEY `idx_criteria_rubric` (`rubric_id`),
  CONSTRAINT `fk_criteria_rubric` FOREIGN KEY (`rubric_id`) REFERENCES `rubrics` (`rubric_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `rubric_criteria`
--

LOCK TABLES `rubric_criteria` WRITE;
/*!40000 ALTER TABLE `rubric_criteria` DISABLE KEYS */;
/*!40000 ALTER TABLE `rubric_criteria` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `rubrics`
--

DROP TABLE IF EXISTS `rubrics`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rubrics` (
  `rubric_id` int NOT NULL AUTO_INCREMENT,
  `question_id` int NOT NULL,
  `total_score` decimal(5,2) NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`rubric_id`),
  UNIQUE KEY `question_id` (`question_id`),
  CONSTRAINT `fk_rubrics_question` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `rubrics`
--

LOCK TABLES `rubrics` WRITE;
/*!40000 ALTER TABLE `rubrics` DISABLE KEYS */;
/*!40000 ALTER TABLE `rubrics` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `user_id` int NOT NULL AUTO_INCREMENT,
  `role_id` int NOT NULL,
  `full_name` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` enum('Active','Inactive','Locked') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Active',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `email` (`email`),
  KEY `idx_users_role` (`role_id`),
  CONSTRAINT `fk_users_role` FOREIGN KEY (`role_id`) REFERENCES `roles` (`role_id`)
) ENGINE=InnoDB AUTO_INCREMENT=28 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
INSERT INTO `users` VALUES (1,1,'System Administrator','admin@aives.com','$2a$11$oTi0NqE6uVmnnLmtGZkXmOqzAudD41nJBIcHfv3xpU.7bzGuFWaHC','Active','2026-09-27 21:59:15','2026-09-28 08:36:45'),(2,2,'Nguyen Van Lecturer','lecturer@aives.com','$2a$11$1hChsIYnjEIgk0SxbBCDo.Ue9PRllizQd4KHmAP4J9rkL84x5z0Wq','Active','2026-09-27 21:59:15','2026-09-28 08:36:47'),(3,3,'Tran Van Student','student@aives.com','$2a$11$NDwCCBFfQr8juGx9YYfj0uG5SkXDUvE3bIpf1HsGTgjmeaZOqoYMy','Active','2026-09-27 21:59:15','2026-09-28 08:36:49'),(4,3,'Gia Thuan','giathuan123@gmail.com','$2a$11$iDzMM39DGDkvATj.KT4xgOs5WB/AZmApLefaXG8VDW7U8/7k1MwlO','Active','2026-09-28 20:53:12',NULL),(5,2,'Giảng viên 2','lecturer2@aives.com','$2a$11$WCrh8weBtif0DCp.1kdfV.blindY1TXYmDt7/5i.thXkNLymExiJ2','Active','2026-09-28 16:57:43','2026-09-28 17:02:47'),(6,3,'Tuan Anh','tuananh@gmail.com','$2a$11$tP2Z7luLNrKq8Itc/rjQuuKZHsnMn90AoATbUcp6AozThiN5YUOdu','Active','2026-09-29 12:21:07',NULL),(7,3,'Anh Thi','anhthi@gmail.com','$2a$11$acS7vUaSPoQkphBS4kfSOeJj1RZCbgDDZL1NurPP9/V1NkSftUTZK','Active','2026-09-29 12:22:14',NULL),(8,3,'Minh Quoc','minhquoc@gmail.com','$2a$11$eO49KfTts768/V95EXZw3ulEakkTXT4GnwmpUWZ4OEmfeUC9vmbbe','Active','2026-09-29 12:22:36',NULL),(9,3,'Nguyen Van Kha','vankha@gmail.com','$2a$11$65iusZ9tKIFS16gLHP03yef4wBNIDykAP7Ynv/vYT4xtPIZEwRygK','Active','2026-09-29 12:22:56',NULL),(13,3,'bao hung','hung@gmail.com','$2a$11$fj4cG8LYF0kaC7Dviq.v3un6SB/GMqcD8rQb8FKkVEaBmUfyXNyDS','Active','2026-09-29 14:49:05',NULL),(14,3,'Trí','jobtrimng05@gmail.com','$2a$11$M86TimD7CYnStCCHlHa4H.YgEV2grpexyEaCvw3MdaIBbtXeOuzc6','Active','2026-09-29 15:26:33',NULL),(21,2,'[TFLOW] Giảng viên A','tflow.gv1@test.local','$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6','Active','2026-09-29 23:29:00',NULL),(22,2,'[TFLOW] Giảng viên B','tflow.gv2@test.local','$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6','Active','2026-09-29 23:29:00',NULL),(23,3,'[TFLOW] Sinh viên 1','tflow.sv1@test.local','$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6','Active','2026-09-29 23:29:00',NULL),(24,3,'[TFLOW] Sinh viên 2','tflow.sv2@test.local','$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6','Active','2026-09-29 23:29:00',NULL),(25,3,'[TFLOW] Sinh viên 3','tflow.sv3@test.local','$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6','Active','2026-09-29 23:29:00',NULL),(26,3,'[TFLOW] Sinh viên 4','tflow.sv4@test.local','$2a$11$8PmlyGyZY2xC80Fe3ZC0h.N1xqHXXdBQt4atZnQKBnwYmO..//Sm6','Active','2026-09-29 23:29:00',NULL),(27,3,'Hoàng Thái','phulon1212@gmail.com','$2a$11$0MM2y7vwY6W9UoXbZqbpzOGkSZ69j..ksaf5tsGJBjgCr1gU5qf6O','Active','2026-10-01 18:50:46',NULL);
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Dumping routines for database 'defaultdb'
--
SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-10-02  9:35:16
