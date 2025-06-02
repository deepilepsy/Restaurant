CREATE DATABASE RestaurantDB;
GO

USE RestaurantDB;
GO

-- Staff
CREATE TABLE dbo.staff (
                           staff_id INT PRIMARY KEY IDENTITY(1,1),
                           name NVARCHAR(255) NOT NULL,
                           surname NVARCHAR(255) NOT NULL,
                           job NVARCHAR(25) NOT NULL,
                           tel_no NVARCHAR(25) NOT NULL
);

-- Restaurant tables
CREATE TABLE dbo.restaurant_tables (
                                       table_id INT PRIMARY KEY,
                                       min_capacity INT NOT NULL,
                                       max_capacity INT NOT NULL,
                                       served_by_id INT NOT NULL,
                                       CONSTRAINT FK_table_staff FOREIGN KEY (served_by_id)
                                           REFERENCES dbo.staff(staff_id)
);

-- Customers
CREATE TABLE dbo.customers (
                               customer_id INT PRIMARY KEY IDENTITY (1, 1),
                               name NVARCHAR(255) NOT NULL,
                               surname NVARCHAR(255) NOT NULL,
                               tel_no NVARCHAR(25) NOT NULL,
                               email NVARCHAR(255) NULL
);

-- Reservation details
CREATE TABLE dbo.reservation_details (
                                         res_details_id INT PRIMARY KEY IDENTITY (1, 1),
                                         guest_number INT NOT NULL,
                                         created_at DATETIME DEFAULT GETDATE(),
                                         reservation_date DATE NOT NULL,
                                         reservation_hour NVARCHAR(10) NOT NULL,
                                         reservation_status NVARCHAR(10) NOT NULL DEFAULT 'active',
                                         special_requests NVARCHAR(MAX) NULL
);

-- Reservations table
CREATE TABLE dbo.reservations (
                                  reservation_id INT PRIMARY KEY IDENTITY(600000,1),
                                  res_details_id INT NOT NULL,
                                  customer_id INT NOT NULL,
                                  table_id INT NOT NULL,
                                  CONSTRAINT FK_reservation_customer FOREIGN KEY (customer_id)
                                      REFERENCES dbo.customers(customer_id),
                                  CONSTRAINT FK_reservation_table FOREIGN KEY (table_id)
                                      REFERENCES dbo.restaurant_tables(table_id),
                                  CONSTRAINT FK_reservation_detail FOREIGN KEY (res_details_id)
                                      REFERENCES dbo.reservation_details(res_details_id)
);

-- Credentials
CREATE TABLE dbo.user_credentials (
                                      id INT PRIMARY KEY IDENTITY(1, 1),
                                      username NVARCHAR(16) NOT NULL UNIQUE,
                                      password NVARCHAR(16) NOT NULL,
                                      user_type NVARCHAR(10) NOT NULL
);

-- Menu items
CREATE TABLE dbo.menu_items (
                                item_id INT PRIMARY KEY IDENTITY (1,1),
                                item_name NVARCHAR(255) NOT NULL,
                                category NVARCHAR(50) NOT NULL,
                                subcategory NVARCHAR(50) NULL,
                                price NVARCHAR(20) NOT NULL,
                                calories INT NULL
);

-- Receipts
CREATE TABLE dbo.receipts (
                              receipt_id INT PRIMARY KEY IDENTITY(1,1),
                              reservation_id INT NOT NULL,
                              staff_id INT NOT NULL,
                              total_amount NVARCHAR(20) NOT NULL,
                              created_at DATETIME DEFAULT GETDATE(),
                              CONSTRAINT FK_receipt_reservation FOREIGN KEY (reservation_id)
                                  REFERENCES dbo.reservations(reservation_id),
                              CONSTRAINT FK_receipt_staff FOREIGN KEY (staff_id)
                                  REFERENCES dbo.staff(staff_id)
);

-- Receipt items
CREATE TABLE dbo.receipt_items (
                                   receipt_item_id INT PRIMARY KEY IDENTITY(1,1),
                                   receipt_id INT NOT NULL,
                                   item_id INT NOT NULL,
                                   quantity INT NOT NULL,
                                   unit_price NVARCHAR(20) NOT NULL,
                                   special_notes NVARCHAR(MAX) NULL,
                                   CONSTRAINT FK_receipt_item_receipt FOREIGN KEY (receipt_id)
                                       REFERENCES dbo.receipts(receipt_id),
                                   CONSTRAINT FK_receipt_item_menu FOREIGN KEY (item_id)
                                       REFERENCES dbo.menu_items(item_id)
);



-- Insert credentials
INSERT INTO dbo.user_credentials (username, password, user_type) VALUES
                                                                     ('root', 'root', 'admin'),
                                                                     ('staff', 'staff', 'staff');

-- Insert staff
INSERT INTO dbo.staff (name, surname, job, tel_no) VALUES
                                                       (N'Ali', N'Yılmaz', 'waiter', N'+905001112233'),
                                                       (N'Ayşe', N'Demir', 'waiter', N'+905002223344'),
                                                       (N'Mehmet', N'Kara', 'waiter', N'+905003334455'),
                                                       (N'Zeynep', N'Çelik', 'waiter', N'+905004445566'),
                                                       (N'Ahmet', N'Şahin', 'chef', N'+905005556677'),
                                                       (N'Elif', N'Koç', 'chef', N'+905006667788'),
                                                       (N'Mert', N'Aydın', 'cleaner', N'+905007778899'),
                                                       (N'Seda', N'Öztürk', 'cleaner', N'+905008889900'),
                                                       (N'Can', N'Güneş', 'dishwasher', N'+905009990011'),
                                                       (N'Esra', N'Bozkurt', 'dishwasher', N'+905001001002');

-- Insert tables
INSERT INTO dbo.restaurant_tables (table_id, min_capacity, max_capacity, served_by_id) VALUES
                                                                                           (1, 5, 10, 1), (2, 5, 10, 1), (3, 3, 4, 1), (4, 3, 4, 1), (5, 3, 4, 1),
                                                                                           (6, 3, 4, 2), (7, 3, 4, 2), (8, 3, 4, 2), (9, 3, 4, 2), (10, 3, 4, 2),
                                                                                           (11, 2, 2, 3), (12, 2, 2, 3), (13, 2, 2, 3), (14, 2, 2, 3), (15, 2, 2, 3),
                                                                                           (16, 1, 1, 4), (17, 1, 1, 4), (18, 1, 1, 4), (19, 1, 1, 4), (20, 1, 1, 4);

-- DRINKS - Coffees
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Espresso', 'DRINKS', 'Coffees', '145.00 TL', 5),
                                                                                   ('Americano', 'DRINKS', 'Coffees', '148.00 TL', 10),
                                                                                   ('Latte', 'DRINKS', 'Coffees', '152.00 TL', 150),
                                                                                   ('Cappuccino', 'DRINKS', 'Coffees', '152.00 TL', 120),
                                                                                   ('Mocha', 'DRINKS', 'Coffees', '155.00 TL', 250),
                                                                                   ('Flat White', 'DRINKS', 'Coffees', '153.00 TL', 170),
                                                                                   ('Macchiato', 'DRINKS', 'Coffees', '150.00 TL', 90),
                                                                                   ('Frappe', 'DRINKS', 'Coffees', '158.00 TL', 220);

-- DRINKS - Cold Drinks
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Strawberry Lemonade', 'DRINKS', 'Cold Drinks', '145.00 TL', 150),
                                                                                   ('Mango Smoothie', 'DRINKS', 'Cold Drinks', '155.00 TL', 220),
                                                                                   ('Watermelon Cooler', 'DRINKS', 'Cold Drinks', '148.00 TL', 130),
                                                                                   ('Pineapple Mint Juice', 'DRINKS', 'Cold Drinks', '150.00 TL', 140),
                                                                                   ('Blueberry Lemon Fizz', 'DRINKS', 'Cold Drinks', '152.00 TL', 160),
                                                                                   ('Honey Citrus Iced Tea', 'DRINKS', 'Cold Drinks', '140.00 TL', 90),
                                                                                   ('Passion Fruit Soda', 'DRINKS', 'Cold Drinks', '155.00 TL', 170),
                                                                                   ('Tropical Fruit Punch', 'DRINKS', 'Cold Drinks', '160.00 TL', 210);

-- DRINKS - Cocktails
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Strawberry Daiquiri', 'DRINKS', 'Cocktails', '375.00 TL', 210),
                                                                                   ('Mango Margarita', 'DRINKS', 'Cocktails', '380.00 TL', 230),
                                                                                   ('Peach Bellini', 'DRINKS', 'Cocktails', '385.00 TL', 200),
                                                                                   ('Watermelon Mojito', 'DRINKS', 'Cocktails', '378.00 TL', 190),
                                                                                   ('Tropical Rum Punch', 'DRINKS', 'Cocktails', '385.00 TL', 240),
                                                                                   ('Pineapple Coconut Punch', 'DRINKS', 'Cocktails', '382.00 TL', 220),
                                                                                   ('Honey Citrus Mule', 'DRINKS', 'Cocktails', '374.00 TL', 170);

-- DRINKS - Wine
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Cabernet Sauvignon', 'DRINKS', 'Wine', '495.00 TL', 125),
                                                                                   ('Merlot', 'DRINKS', 'Wine', '385.00 TL', 120),
                                                                                   ('Pinot Noir', 'DRINKS', 'Wine', '392.00 TL', 118),
                                                                                   ('Chardonnay', 'DRINKS', 'Wine', '488.00 TL', 115),
                                                                                   ('Sauvignon Blanc', 'DRINKS', 'Wine', '582.00 TL', 110),
                                                                                   ('Prosecco', 'DRINKS', 'Wine', '375.00 TL', 98),
                                                                                   ('Riesling', 'DRINKS', 'Wine', '680.00 TL', 105);

-- STARTERS
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Mini Croque Bites', 'STARTERS', 'Appetizers', '265.00 TL', 180),
                                                                                   ('Stuffed Mushrooms', 'STARTERS', 'Appetizers', '258.00 TL', 150),
                                                                                   ('Bruschetta Trio', 'STARTERS', 'Appetizers', '272.00 TL', 220),
                                                                                   ('Calamari Rings', 'STARTERS', 'Appetizers', '285.00 TL', 280),
                                                                                   ('Chicken Wings', 'STARTERS', 'Appetizers', '278.00 TL', 320),
                                                                                   ('Mozzarella Sticks', 'STARTERS', 'Appetizers', '268.00 TL', 250);

-- SALADS
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Quinoa Superfood', 'SALADS', 'Garden Fresh', '295.00 TL', 380),
                                                                                   ('Caesar Salad', 'SALADS', 'Garden Fresh', '175.00 TL', 280),
                                                                                   ('Greek Salad', 'SALADS', 'Garden Fresh', '182.00 TL', 250),
                                                                                   ('Caprese Salad', 'SALADS', 'Garden Fresh', '188.00 TL', 320),
                                                                                   ('Arugula Walnut', 'SALADS', 'Garden Fresh', '192.00 TL', 290),
                                                                                   ('Asian Sesame', 'SALADS', 'Garden Fresh', '185.00 TL', 240);

-- MAIN COURSES - Steaks & Grills
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Grilled Ribeye Steak', 'MAIN', 'Steaks & Grills', '485.00 TL', 650),
                                                                                   ('Filet Mignon', 'MAIN', 'Steaks & Grills', '420.00 TL', 480),
                                                                                   ('New York Strip', 'MAIN', 'Steaks & Grills', '595.00 TL', 580),
                                                                                   ('Grilled Lamb Chops', 'MAIN', 'Steaks & Grills', '510.00 TL', 520);

-- MAIN COURSES - Seafood
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Salmon Teriyaki', 'MAIN', 'Seafood', '365.00 TL', 420),
                                                                                   ('Pan Seared Halibut', 'MAIN', 'Seafood', '475.00 TL', 380),
                                                                                   ('Lobster Tail', 'MAIN', 'Seafood', '540.00 TL', 320),
                                                                                   ('Seafood Paella', 'MAIN', 'Seafood', '585.00 TL', 480);

-- MAIN COURSES - Pasta & Italian
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Spaghetti Carbonara', 'MAIN', 'Pasta & Italian', '325.00 TL', 580),
                                                                                   ('Chicken Alfredo', 'MAIN', 'Pasta & Italian', '335.00 TL', 650),
                                                                                   ('Penne Arrabbiata', 'MAIN', 'Pasta & Italian', '315.00 TL', 420),
                                                                                   ('Mushroom Risotto', 'MAIN', 'Pasta & Italian', '328.00 TL', 450);

-- SIDES
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Truffle Fries', 'SIDES', 'Accompaniments', '255.00 TL', 380),
                                                                                   ('Garlic Mashed Potatoes', 'SIDES', 'Accompaniments', '245.00 TL', 280),
                                                                                   ('Grilled Asparagus', 'SIDES', 'Accompaniments', '248.00 TL', 120),
                                                                                   ('Roasted Brussels Sprouts', 'SIDES', 'Accompaniments', '252.00 TL', 180),
                                                                                   ('Wild Rice Pilaf', 'SIDES', 'Accompaniments', '242.00 TL', 220),
                                                                                   ('Sautéed Spinach', 'SIDES', 'Accompaniments', '238.00 TL', 95);

-- DESSERTS
INSERT INTO dbo.menu_items (item_name, category, subcategory, price, calories) VALUES
                                                                                   ('Chocolate Fondant', 'DESSERTS', 'Sweet Endings', '285.00 TL', 450),
                                                                                   ('Tiramisu', 'DESSERTS', 'Sweet Endings', '275.00 TL', 380),
                                                                                   ('Crème Brûlée', 'DESSERTS', 'Sweet Endings', '278.00 TL', 320),
                                                                                   ('New York Cheesecake', 'DESSERTS', 'Sweet Endings', '282.00 TL', 420),
                                                                                   ('Gelato Trio', 'DESSERTS', 'Sweet Endings', '265.00 TL', 280),
                                                                                   ('Apple Tart', 'DESSERTS', 'Sweet Endings', '272.00 TL', 350);

