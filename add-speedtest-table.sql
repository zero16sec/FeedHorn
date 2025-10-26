-- SQL script to add SpeedTests table to existing FeedHorn database
-- Run this if you want to keep existing URL monitoring data

CREATE TABLE IF NOT EXISTS "SpeedTests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SpeedTests" PRIMARY KEY AUTOINCREMENT,
    "TestedAt" TEXT NOT NULL,
    "DownloadMbps" REAL NOT NULL,
    "UploadMbps" REAL NOT NULL,
    "PingMs" INTEGER NOT NULL,
    "JitterMs" INTEGER NOT NULL,
    "ServerName" TEXT NULL,
    "ServerLocation" TEXT NULL,
    "Isp" TEXT NULL,
    "IsSuccess" INTEGER NOT NULL,
    "ErrorMessage" TEXT NULL
);
