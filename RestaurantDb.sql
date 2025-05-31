
CREATE DATABASE RestaurantDB;
GO

USE RestaurantDB;
GO

CREATE TABLE dbo.staff (
                           staff_id INT PRIMARY KEY IDENTITY(1,1),
                           name NVARCHAR(255) NOT NULL,
                           surname NVARCHAR(255),
                           job NVARCHAR(25),
                           tel_no NVARCHAR(25)
);

CREATE TABLE dbo.restaurant_tables (
                                       table_id INT PRIMARY KEY,
                                       min_capacity INT NOT NULL,
                                       max_capacity INT NOT NULL,
                                       served_by_id INT NOT NULL,
                                       CONSTRAINT FK_table_staff FOREIGN KEY (served_by_id)
                                           REFERENCES dbo.staff(staff_id)
);

CREATE TABLE dbo.customers (
                                       customer_id INT PRIMARY KEY IDENTITY (1, 1),
                                       name NVARCHAR(255) NOT NULL,
                                       surname NVARCHAR(255),
                                       tel_no NVARCHAR(25) NOT NULL,
                                       email NVARCHAR(255) NULL,
);

CREATE TABLE dbo.reservations (
                                  reservation_id INT PRIMARY KEY IDENTITY(600000,1),
                                customer_id INT,
                                  guest_number INT NOT NULL,
                                  served_by_id INT NOT NULL,
                                  table_id INT NOT NULL,
                                  created_at DATETIME DEFAULT GETDATE(),
                                  reservation_date DATE NOT NULL,
                                  reservation_hour NVARCHAR(10) NOT NULL,
                                  reservation_status NVARCHAR(10) NOT NULL DEFAULT 'active',
                                  special_requests NVARCHAR(MAX) NULL,
                                    CONSTRAINT FK_reservation_customer FOREIGN KEY (customer_id)
                                        REFERENCES dbo.customers(customer_id),
                                  CONSTRAINT FK_reservation_table FOREIGN KEY (table_id)
                                      REFERENCES dbo.restaurant_tables(table_id),
                                  CONSTRAINT FK_reservation_staff FOREIGN KEY (served_by_id)
                                      REFERENCES dbo.staff(staff_id)
);

CREATE TABLE dbo.credentials (
                                 id INT PRIMARY KEY IDENTITY(1, 1),
                                 username NVARCHAR(16),
                                 password NVARCHAR(16),
);

CREATE TABLE dbo.staff_credentials (
                                       id INT PRIMARY KEY IDENTITY(1, 1),
                                       username NVARCHAR(16),
                                       password NVARCHAR(16),
);

INSERT INTO dbo.credentials (username, password) VALUES ('root', 'root');
INSERT INTO dbo.staff_credentials (username, password) VALUES ('staff', 'staff');

INSERT INTO dbo.staff (name, surname, job, tel_no) VALUES
                                                       (N'Ali', N'Yılmaz', 'waiter', N'05001112233'),
                                                       (N'Ayşe', N'Demir', 'waiter', N'05002223344'),
                                                       (N'Mehmet', N'Kara', 'waiter', N'05003334455'),
                                                       (N'Zeynep', N'Çelik', 'waiter', N'05004445566'),
                                                       (N'Ahmet', N'Şahin', 'chef', N'05005556677'),
                                                       (N'Elif', N'Koç', 'chef', N'05006667788'),
                                                       (N'Mert', N'Aydın', 'cleaner', N'05007778899'),
                                                       (N'Seda', N'Öztürk', 'cleaner', N'05008889900'),
                                                       (N'Can', N'Güneş', 'dishwasher', N'05009990011'),
                                                       (N'Esra', N'Bozkurt', 'dishwasher', N'05001001002');

INSERT INTO dbo.restaurant_tables (table_id, min_capacity, max_capacity, served_by_id) VALUES
                                                                                           (1, 5, 10, 1),
                                                                                           (2, 5, 10, 1),
                                                                                           (3, 3, 4, 1),
                                                                                           (4, 3, 4, 1),
                                                                                           (5, 3, 4, 1),
                                                                                           (6, 3, 4, 2),
                                                                                           (7, 3, 4, 2),
                                                                                           (8, 3, 4, 2),
                                                                                           (9, 3, 4, 2),
                                                                                           (10, 3, 4, 2),
                                                                                           (11, 2, 2, 3),
                                                                                           (12, 2, 2, 3),
                                                                                           (13, 2, 2, 3),
                                                                                           (14, 2, 2, 3),
                                                                                           (15, 2, 2, 3),
                                                                                           (16, 1, 1, 4),
                                                                                           (17, 1, 1, 4),
                                                                                           (18, 1, 1, 4),
                                                                                           (19, 1, 1, 4),
                                                                                           (20, 1, 1, 4);
