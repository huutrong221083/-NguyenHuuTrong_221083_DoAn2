USE [QuanLy_HieuSuat]
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRAN;

    /*
      1) Standardize task status columns
         Canonical status is MATRANGTHAI (FK to TRANGTHAICONGVIEC).
         Keep TRANGTHAI in sync for backward compatibility with existing code.
    */

    -- Backfill MATRANGTHAI from TRANGTHAI when possible
    UPDATE c
    SET c.MATRANGTHAI = c.TRANGTHAI
    FROM dbo.CONGVIEC c
    WHERE c.TRANGTHAI IS NOT NULL
      AND c.TRANGTHAI BETWEEN 1 AND 5
      AND c.MATRANGTHAI <> c.TRANGTHAI;

    -- Backfill TRANGTHAI from MATRANGTHAI
    UPDATE c
    SET c.TRANGTHAI = c.MATRANGTHAI
    FROM dbo.CONGVIEC c
    WHERE c.TRANGTHAI IS NULL
      OR c.TRANGTHAI <> c.MATRANGTHAI;

    -- Set safe default for legacy writes missing TRANGTHAI
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns col ON col.default_object_id = dc.object_id
        JOIN sys.tables t ON t.object_id = col.object_id
        WHERE t.name = 'CONGVIEC' AND col.name = 'TRANGTHAI'
    )
    BEGIN
        ALTER TABLE dbo.CONGVIEC
        ADD CONSTRAINT DF_CONGVIEC_TRANGTHAI DEFAULT (1) FOR TRANGTHAI;
    END;

    -- Enforce status range on TRANGTHAI
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_CONGVIEC_TRANGTHAI_RANGE'
    )
    BEGIN
        ALTER TABLE dbo.CONGVIEC
        ADD CONSTRAINT CK_CONGVIEC_TRANGTHAI_RANGE CHECK (TRANGTHAI BETWEEN 1 AND 5);
    END;

    -- Keep TRANGTHAI synchronized with MATRANGTHAI
    IF OBJECT_ID('dbo.TR_CONGVIEC_SYNC_TRANGTHAI', 'TR') IS NULL
    EXEC('
        CREATE TRIGGER dbo.TR_CONGVIEC_SYNC_TRANGTHAI
        ON dbo.CONGVIEC
        AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            UPDATE c
            SET c.TRANGTHAI = c.MATRANGTHAI
            FROM dbo.CONGVIEC c
            INNER JOIN inserted i ON i.MACONGVIEC = c.MACONGVIEC
            WHERE c.TRANGTHAI <> c.MATRANGTHAI OR c.TRANGTHAI IS NULL;
        END
    ');

    /*
      2) Add missing foreign keys
    */

        -- Clean orphan comment owners before adding FK
        UPDATE b
        SET b.MANHANVIEN = NULL
        FROM dbo.BINHLUAN b
        LEFT JOIN dbo.NHANVIEN n ON n.MANHANVIEN = b.MANHANVIEN
        WHERE b.MANHANVIEN IS NOT NULL
            AND n.MANHANVIEN IS NULL;

        -- Clean orphan department heads before adding FK
        UPDATE p
        SET p.MATRUONGPHONG = NULL
        FROM dbo.PHONGBAN p
        LEFT JOIN dbo.NHANVIEN n ON n.MANHANVIEN = p.MATRUONGPHONG
        WHERE p.MATRUONGPHONG IS NOT NULL
            AND n.MANHANVIEN IS NULL;

        -- Clean orphan project targets before adding FK
        UPDATE d
        SET d.MADOITUONG = NULL
        FROM dbo.DUDOANAI d
        LEFT JOIN dbo.DUAN a ON a.MADUAN = d.MADOITUONG
        WHERE d.MADOITUONG IS NOT NULL
            AND a.MADUAN IS NULL;

    -- BINHLUAN.MANHANVIEN -> NHANVIEN.MANHANVIEN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_BINHLUAN_NHANVIEN'
    )
    BEGIN
        ALTER TABLE dbo.BINHLUAN WITH CHECK
        ADD CONSTRAINT FK_BINHLUAN_NHANVIEN
        FOREIGN KEY (MANHANVIEN)
        REFERENCES dbo.NHANVIEN (MANHANVIEN);
    END;

    -- PHONGBAN.MATRUONGPHONG -> NHANVIEN.MANHANVIEN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_PHONGBAN_TRUONGPHONG_NHANVIEN'
    )
    BEGIN
        ALTER TABLE dbo.PHONGBAN WITH CHECK
        ADD CONSTRAINT FK_PHONGBAN_TRUONGPHONG_NHANVIEN
        FOREIGN KEY (MATRUONGPHONG)
        REFERENCES dbo.NHANVIEN (MANHANVIEN);
    END;

    -- DUDOANAI.MADOITUONG -> DUAN.MADUAN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_DUDOANAI_DUAN_MADOITUONG'
    )
    BEGIN
        ALTER TABLE dbo.DUDOANAI WITH CHECK
        ADD CONSTRAINT FK_DUDOANAI_DUAN_MADOITUONG
        FOREIGN KEY (MADOITUONG)
        REFERENCES dbo.DUAN (MADUAN);
    END;

    /*
      3) Prevent duplicate assignments for the same task-employee pair
    */

    ;WITH duplicate_assignments AS (
        SELECT
            MAPHANCONG,
            ROW_NUMBER() OVER (
                PARTITION BY MACONGVIEC, MANHANVIEN
                ORDER BY MAPHANCONG
            ) AS rn
        FROM dbo.PHANCONGCONGVIEC
        WHERE MANHANVIEN IS NOT NULL
    )
    DELETE FROM duplicate_assignments
    WHERE rn > 1;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_PHANCONGCONGVIEC_MACONGVIEC_MANHANVIEN'
          AND object_id = OBJECT_ID('dbo.PHANCONGCONGVIEC')
    )
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX UX_PHANCONGCONGVIEC_MACONGVIEC_MANHANVIEN
        ON dbo.PHANCONGCONGVIEC (MACONGVIEC, MANHANVIEN)
        WHERE MANHANVIEN IS NOT NULL;
    END;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
GO

PRINT 'Normalize_Schema.sql completed successfully.';
GO
