-- Errors
CREATE TABLE "Errors" (
    "Id" SERIAL PRIMARY KEY,
    "FileName" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "LinePosition" BIGINT NOT NULL,
    "ContentHash" CHAR(64) NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX "IX_Errors_FileName_LinePosition" ON "Errors" ("FileName", "LinePosition");
CREATE INDEX "IX_Errors_CreatedAt" ON "Errors" ("CreatedAt");

-- Notifications
CREATE TABLE "Notifications" (
    "Id" SERIAL PRIMARY KEY,
    "ErrorId" INT NOT NULL REFERENCES "Errors"("Id") ON DELETE CASCADE
    "SentAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "EmailSent" BOOLEAN NOT NULL DEFAULT FALSE,
    "TelegramSent" BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX "IX_Notifications_ErrorId" ON "Notifications" ("ErrorId");

-- FilePositions (для отслеживания прогресса чтения)
CREATE TABLE "FilePositions" (
    "FilePath" TEXT PRIMARY KEY,
    "LastPosition" BIGINT NOT NULL
);
-- TelegramSubscribers (id чатов с ботом)
CREATE TABLE "TelegramSubscribers" (
    "ChatId" BIGINT PRIMARY KEY,      -- ID чата (может быть отрицательным для групп, но мы будем брать только личные — положительные)
    "FirstName" TEXT,
    "Username" TEXT,
    "SubscribedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
    "IsActive" BOOLEAN NOT NULL DEFAULT true
);