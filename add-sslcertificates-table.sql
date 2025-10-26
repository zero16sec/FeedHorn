-- SQL script to add SslCertificates table to existing FeedHorn database
-- Run this if you want to keep existing URL monitoring and speed test data

CREATE TABLE IF NOT EXISTS "SslCertificates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SslCertificates" PRIMARY KEY AUTOINCREMENT,
    "FriendlyName" TEXT NOT NULL,
    "Url" TEXT NOT NULL,
    "Issuer" TEXT NOT NULL,
    "Subject" TEXT NOT NULL,
    "ValidFrom" TEXT NOT NULL,
    "ValidTo" TEXT NOT NULL,
    "LastChecked" TEXT NOT NULL,
    "IsValid" INTEGER NOT NULL,
    "ErrorMessage" TEXT NULL,
    "DaysUntilExpiration" INTEGER NOT NULL
);
