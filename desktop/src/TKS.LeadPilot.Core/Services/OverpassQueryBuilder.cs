using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public static class OverpassQueryBuilder
{
    public static readonly Dictionary<string, BoundingBox> CityBoundingBoxes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lahore"] = new(31.40, 74.15, 31.65, 74.45),
        ["Faisalabad"] = new(31.35, 73.00, 31.50, 73.18),
        ["Rawalpindi"] = new(33.50, 73.00, 33.65, 73.15),
        ["Multan"] = new(30.12, 71.40, 30.25, 71.55),
        ["Gujranwala"] = new(32.12, 74.12, 32.25, 74.26),
        ["Sialkot"] = new(32.44, 74.46, 32.56, 74.58),
        ["Sargodha"] = new(32.02, 72.60, 32.15, 72.75),
        ["Bahawalpur"] = new(29.34, 71.62, 29.46, 71.76),
        ["Sheikhupura"] = new(31.65, 73.90, 31.78, 74.05),
        ["Gujrat"] = new(32.52, 74.01, 32.64, 74.15),
        ["Jhang"] = new(31.21, 72.25, 31.33, 72.39),
        ["Sahiwal"] = new(30.61, 73.04, 30.73, 73.18),
        ["Rahim Yar Khan"] = new(28.36, 70.24, 28.48, 70.37),
        ["Kasur"] = new(31.06, 74.39, 31.18, 74.52),
        ["Okara"] = new(30.75, 73.38, 30.87, 73.51),
        ["Dera Ghazi Khan"] = new(30.00, 70.57, 30.12, 70.71),
        ["Attock"] = new(33.72, 72.30, 33.82, 72.42),
        ["Chakwal"] = new(32.88, 72.80, 32.98, 72.92),
        ["Jhelum"] = new(32.89, 73.68, 32.99, 73.78),
        ["Taxila"] = new(33.70, 72.78, 33.80, 72.90),
        ["Wah Cantt"] = new(33.72, 72.70, 33.82, 72.80),
        ["Mandi Bahauddin"] = new(32.53, 73.44, 32.64, 73.54),
        ["Hafizabad"] = new(32.02, 73.63, 32.12, 73.74),
        ["Chiniot"] = new(31.67, 72.92, 31.77, 73.03),
        ["Toba Tek Singh"] = new(30.92, 72.43, 31.02, 72.53),
        ["Vehari"] = new(29.99, 72.29, 30.09, 72.40),
        ["Khanewal"] = new(30.25, 71.88, 30.35, 71.98),
        ["Lodhran"] = new(29.49, 71.58, 29.59, 71.69),
        ["Pakpattan"] = new(30.29, 73.33, 30.39, 73.44),
        ["Bahawalnagar"] = new(29.94, 73.20, 30.05, 73.31),
        ["Muzaffargarh"] = new(30.02, 71.14, 30.13, 71.25),
        ["Layyah"] = new(30.91, 70.89, 31.01, 71.00),
        ["Bhakkar"] = new(31.57, 71.01, 31.68, 71.12),
        ["Mianwali"] = new(32.53, 71.49, 32.64, 71.60),
        ["Khushab"] = new(32.24, 72.30, 32.35, 72.41),
        ["Murree"] = new(33.86, 73.34, 33.96, 73.44),
        ["Kot Addu"] = new(30.41, 70.91, 30.52, 71.02),
        ["Taunsa"] = new(30.65, 70.60, 30.76, 70.70),
        ["Karachi"] = new(24.75, 66.85, 25.10, 67.25),
        ["Hyderabad"] = new(25.32, 68.28, 25.45, 68.42),
        ["Sukkur"] = new(27.65, 68.80, 27.76, 68.92),
        ["Larkana"] = new(27.50, 68.15, 27.62, 68.28),
        ["Nawabshah"] = new(26.19, 68.35, 26.31, 68.48),
        ["Mirpur Khas"] = new(25.47, 68.95, 25.59, 69.08),
        ["Jacobabad"] = new(28.22, 68.37, 28.34, 68.50),
        ["Thatta"] = new(24.69, 67.87, 24.80, 67.98),
        ["Badin"] = new(24.60, 68.78, 24.71, 68.89),
        ["Tando Allahyar"] = new(25.41, 68.66, 25.51, 68.77),
        ["Tando Muhammad Khan"] = new(25.07, 68.48, 25.18, 68.59),
        ["Matiari"] = new(25.54, 68.39, 25.65, 68.50),
        ["Jamshoro"] = new(25.38, 68.23, 25.48, 68.34),
        ["Dadu"] = new(26.68, 67.72, 26.78, 67.83),
        ["Sanghar"] = new(25.99, 68.89, 26.10, 69.00),
        ["Naushahro Feroze"] = new(26.79, 68.07, 26.89, 68.18),
        ["Ghotki"] = new(27.95, 69.26, 28.06, 69.37),
        ["Shikarpur"] = new(27.90, 68.58, 28.01, 68.69),
        ["Kandhkot"] = new(28.19, 69.13, 28.30, 69.24),
        ["Kashmore"] = new(28.38, 69.53, 28.49, 69.64),
        ["Kambar Shahdadkot"] = new(27.54, 67.94, 27.65, 68.05),
        ["Umerkot"] = new(25.31, 69.68, 25.42, 69.79),
        ["Mithi"] = new(24.69, 69.75, 24.80, 69.85),
        ["Islamabad"] = new(33.60, 72.95, 33.78, 73.18),
        ["Peshawar"] = new(33.95, 71.45, 34.05, 71.60),
        ["Abbottabad"] = new(34.12, 73.18, 34.22, 73.28),
        ["Mardan"] = new(34.14, 71.97, 34.26, 72.11),
        ["Swat"] = new(34.70, 72.30, 34.84, 72.43),
        ["Kohat"] = new(33.52, 71.37, 33.65, 71.51),
        ["Dera Ismail Khan"] = new(31.77, 70.83, 31.89, 70.97),
        ["Haripur"] = new(33.93, 72.86, 34.06, 73.00),
        ["Mansehra"] = new(34.27, 73.13, 34.39, 73.26),
        ["Swabi"] = new(34.07, 72.41, 34.18, 72.53),
        ["Nowshera"] = new(33.96, 71.92, 34.07, 72.03),
        ["Charsadda"] = new(34.09, 71.67, 34.20, 71.79),
        ["Bannu"] = new(32.93, 70.55, 33.04, 70.66),
        ["Karak"] = new(33.06, 71.04, 33.17, 71.15),
        ["Hangu"] = new(33.48, 71.00, 33.59, 71.11),
        ["Malakand"] = new(34.51, 71.86, 34.62, 71.97),
        ["Dir"] = new(35.15, 71.82, 35.26, 71.93),
        ["Chitral"] = new(35.80, 71.73, 35.91, 71.84),
        ["Batagram"] = new(34.62, 72.97, 34.73, 73.08),
        ["Shangla"] = new(34.83, 72.70, 34.94, 72.81),
        ["Buner"] = new(34.34, 72.56, 34.45, 72.67),
        ["Bajaur"] = new(34.73, 71.47, 34.84, 71.58),
        ["Mohmand"] = new(34.48, 71.38, 34.59, 71.49),
        ["Khyber"] = new(33.95, 71.10, 34.06, 71.21),
        ["Kurram"] = new(33.85, 70.05, 33.95, 70.15),
        ["Tank"] = new(32.17, 70.32, 32.28, 70.43),
        ["Quetta"] = new(30.12, 66.92, 30.24, 67.04),
        ["Gwadar"] = new(25.08, 62.28, 25.18, 62.38),
        ["Turbat"] = new(25.95, 62.98, 26.07, 63.12),
        ["Khuzdar"] = new(27.74, 66.55, 27.86, 66.68),
        ["Hub"] = new(24.84, 66.82, 24.96, 66.95),
        ["Chaman"] = new(30.87, 66.40, 30.98, 66.51),
        ["Zhob"] = new(31.29, 69.39, 31.40, 69.50),
        ["Loralai"] = new(30.32, 68.54, 30.43, 68.65),
        ["Pishin"] = new(30.53, 66.95, 30.64, 67.06),
        ["Killa Abdullah"] = new(30.67, 66.61, 30.78, 66.72),
        ["Sibi"] = new(29.49, 67.82, 29.60, 67.93),
        ["Dera Bugti"] = new(28.98, 69.13, 29.09, 69.24),
        ["Kohlu"] = new(29.84, 69.20, 29.95, 69.31),
        ["Jaffarabad"] = new(28.38, 68.20, 28.49, 68.31),
        ["Nasirabad"] = new(28.53, 68.13, 28.64, 68.24),
        ["Kharan"] = new(28.53, 65.36, 28.64, 65.47),
        ["Panjgur"] = new(26.91, 64.04, 27.02, 64.15),
        ["Lasbela"] = new(25.70, 66.55, 25.80, 66.65),
        ["Uthal"] = new(25.75, 66.57, 25.86, 66.67),
        ["Kalat"] = new(28.97, 66.54, 29.08, 66.65),
        ["Mastung"] = new(29.74, 66.79, 29.85, 66.90),
        ["Ziarat"] = new(30.33, 67.67, 30.44, 67.78),
        ["Harnai"] = new(30.05, 67.88, 30.16, 67.99),
        ["Nushki"] = new(29.50, 65.97, 29.61, 66.08),
        ["Gilgit"] = new(35.86, 74.25, 35.98, 74.38),
        ["Skardu"] = new(35.24, 75.56, 35.36, 75.70),
        ["Hunza"] = new(36.26, 74.58, 36.38, 74.72),
        ["Chilas"] = new(35.37, 74.04, 35.48, 74.15),
        ["Gahkuch"] = new(36.12, 73.71, 36.23, 73.82),
        ["Astore"] = new(35.31, 74.80, 35.42, 74.91),
        ["Khaplu"] = new(35.10, 76.28, 35.21, 76.39),
        ["Shigar"] = new(35.37, 75.67, 35.48, 75.78),
        ["Nagar"] = new(36.20, 74.61, 36.31, 74.72),
        ["Kharmang"] = new(34.88, 76.16, 34.99, 76.27),
        ["Muzaffarabad"] = new(34.31, 73.41, 34.43, 73.54),
        ["Mirpur"] = new(33.09, 73.68, 33.21, 73.82),
        ["Rawalakot"] = new(33.80, 73.70, 33.92, 73.83),
        ["Kotli"] = new(33.46, 73.85, 33.57, 73.96),
        ["Bhimber"] = new(32.92, 74.02, 33.03, 74.13),
        ["Bagh"] = new(33.93, 73.72, 34.03, 73.83),
        ["Haveli"] = new(33.85, 74.05, 33.95, 74.15),
        ["Pallandri"] = new(33.67, 73.63, 33.78, 73.74),
        ["Athmuqam"] = new(34.53, 73.85, 34.64, 73.96),
        ["Hattian Bala"] = new(34.12, 73.69, 34.22, 73.80),
        ["Los Angeles"] = new(33.90, -118.50, 34.20, -118.15),
        ["San Francisco"] = new(37.70, -122.52, 37.83, -122.35),
        ["San Diego"] = new(32.60, -117.28, 32.85, -117.05),
        ["San Jose"] = new(37.25, -122.00, 37.45, -121.75),
        ["Sacramento"] = new(38.50, -121.58, 38.65, -121.40),
        ["Irvine"] = new(33.62, -117.90, 33.75, -117.75),
        ["Oakland"] = new(37.74, -122.34, 37.86, -122.20),
        ["Long Beach"] = new(33.72, -118.25, 33.84, -118.12),
        ["Fresno"] = new(36.68, -119.85, 36.82, -119.70),
        ["Anaheim"] = new(33.78, -117.98, 33.90, -117.84),
        ["Riverside"] = new(33.92, -117.45, 34.04, -117.30),
        ["Bakersfield"] = new(35.30, -119.10, 35.45, -118.94),
        ["Santa Ana"] = new(33.70, -117.92, 33.80, -117.82),
        ["New York City"] = new(40.58, -74.15, 40.85, -73.85),
        ["Buffalo"] = new(42.82, -78.93, 42.96, -78.78),
        ["Rochester"] = new(43.10, -77.68, 43.22, -77.53),
        ["Albany"] = new(42.60, -73.83, 42.70, -73.70),
        ["Syracuse"] = new(43.00, -76.22, 43.10, -76.08),
        ["Yonkers"] = new(40.88, -73.94, 40.98, -73.84),
        ["White Plains"] = new(40.98, -73.82, 41.08, -73.70),
        ["Houston"] = new(29.60, -95.55, 29.90, -95.20),
        ["Austin"] = new(30.15, -97.88, 30.40, -97.62),
        ["Dallas"] = new(32.65, -96.95, 32.90, -96.65),
        ["San Antonio"] = new(29.30, -98.65, 29.55, -98.35),
        ["Fort Worth"] = new(32.68, -97.42, 32.84, -97.24),
        ["El Paso"] = new(31.70, -106.56, 31.84, -106.38),
        ["Arlington"] = new(32.68, -97.18, 32.80, -97.02),
        ["Plano"] = new(32.95, -96.78, 33.09, -96.62),
        ["Corpus Christi"] = new(27.72, -97.48, 27.88, -97.32),
        ["Frisco"] = new(33.10, -96.88, 33.20, -96.76),
        ["Miami"] = new(25.68, -80.30, 25.88, -80.12),
        ["Orlando"] = new(28.40, -81.50, 28.65, -81.25),
        ["Tampa"] = new(27.85, -82.58, 28.08, -82.35),
        ["Jacksonville"] = new(30.22, -81.76, 30.42, -81.55),
        ["Fort Lauderdale"] = new(26.06, -80.20, 26.20, -80.08),
        ["St. Petersburg"] = new(27.70, -82.72, 27.84, -82.58),
        ["Tallahassee"] = new(30.38, -84.35, 30.50, -84.20),
        ["Chicago"] = new(41.70, -87.80, 42.02, -87.55),
        ["Naperville"] = new(41.70, -88.22, 41.80, -88.08),
        ["Rockford"] = new(42.20, -89.18, 42.34, -89.00),
        ["Aurora"] = new(41.70, -88.38, 41.82, -88.24),
        ["Seattle"] = new(47.50, -122.45, 47.72, -122.22),
        ["Bellevue"] = new(47.55, -122.26, 47.68, -122.12),
        ["Spokane"] = new(47.60, -117.50, 47.72, -117.34),
        ["Tacoma"] = new(47.18, -122.52, 47.30, -122.36),
        ["Boston"] = new(42.28, -71.18, 42.42, -70.95),
        ["Cambridge"] = new(42.34, -71.15, 42.40, -71.07),
        ["Worcester"] = new(42.20, -71.88, 42.32, -71.74),
        ["Atlanta"] = new(33.65, -84.50, 33.88, -84.28),
        ["Savannah"] = new(32.00, -81.18, 32.15, -81.02),
        ["Augusta"] = new(33.40, -82.10, 33.54, -81.92),
        ["Denver"] = new(39.62, -105.10, 39.85, -104.85),
        ["Colorado Springs"] = new(38.75, -104.90, 38.92, -104.72),
        ["Boulder"] = new(39.96, -105.34, 40.08, -105.20),
        ["Phoenix"] = new(33.30, -112.25, 33.60, -111.90),
        ["Tucson"] = new(32.15, -111.05, 32.32, -110.88),
        ["Mesa"] = new(33.35, -111.90, 33.48, -111.75),
        ["Scottsdale"] = new(33.42, -111.98, 33.62, -111.85),
        ["Las Vegas"] = new(36.05, -115.30, 36.28, -115.05),
        ["Reno"] = new(39.45, -119.88, 39.60, -119.74),
        ["Philadelphia"] = new(39.88, -75.28, 40.05, -75.05),
        ["Pittsburgh"] = new(40.38, -80.08, 40.50, -79.88),
        ["Allentown"] = new(40.55, -75.56, 40.66, -75.42),
        ["Columbus"] = new(39.88, -83.12, 40.08, -82.88),
        ["Cleveland"] = new(41.42, -81.80, 41.58, -81.60),
        ["Cincinnati"] = new(39.04, -84.60, 39.18, -84.42),
        ["Detroit"] = new(42.25, -83.18, 42.45, -82.95),
        ["Grand Rapids"] = new(42.90, -85.74, 43.02, -85.60),
        ["Ann Arbor"] = new(42.24, -83.80, 42.32, -83.68),
        ["Charlotte"] = new(35.15, -80.95, 35.32, -80.72),
        ["Raleigh"] = new(35.70, -78.75, 35.88, -78.55),
        ["Greensboro"] = new(36.00, -79.88, 36.14, -79.70),
        ["London"] = new(51.40, -0.30, 51.62, 0.05),
        ["Manchester"] = new(53.42, -2.32, 53.54, -2.16),
        ["Birmingham"] = new(52.40, -2.00, 52.55, -1.78),
        ["Leeds"] = new(53.72, -1.65, 53.88, -1.45),
        ["Liverpool"] = new(53.35, -3.05, 53.48, -2.90),
        ["Bristol"] = new(51.40, -2.68, 51.52, -2.50),
        ["Newcastle"] = new(54.92, -1.72, 55.05, -1.52),
        ["Sheffield"] = new(53.32, -1.55, 53.45, -1.38),
        ["Nottingham"] = new(52.90, -1.22, 53.02, -1.08),
        ["Leicester"] = new(52.58, -1.20, 52.70, -1.06),
        ["Oxford"] = new(51.70, -1.32, 51.80, -1.18),
        ["Southampton"] = new(50.85, -1.48, 50.96, -1.32),
        ["Coventry"] = new(52.36, -1.58, 52.46, -1.45),
        ["Edinburgh"] = new(55.88, -3.30, 56.00, -3.10),
        ["Glasgow"] = new(55.80, -4.38, 55.92, -4.15),
        ["Aberdeen"] = new(57.10, -2.18, 57.20, -2.02),
        ["Dundee"] = new(56.42, -3.05, 56.52, -2.88),
        ["Inverness"] = new(57.42, -4.30, 57.52, -4.15),
        ["Cardiff"] = new(51.42, -3.28, 51.55, -3.10),
        ["Swansea"] = new(51.58, -4.02, 51.68, -3.88),
        ["Newport"] = new(51.52, -3.05, 51.64, -2.92),
        ["Belfast"] = new(54.52, -6.02, 54.68, -5.85),
        ["Derry"] = new(54.95, -7.38, 55.05, -7.24),
        ["Lisburn"] = new(54.48, -6.08, 54.56, -5.98),
        ["Dubai"] = new(25.05, 55.10, 25.35, 55.45),
        ["Deira"] = new(25.24, 55.28, 25.30, 55.34),
        ["Business Bay"] = new(25.16, 55.24, 25.21, 55.29),
        ["Jumeirah"] = new(25.14, 55.20, 25.24, 55.28),
        ["Dubai Marina"] = new(25.05, 55.12, 25.12, 55.16),
        ["Abu Dhabi"] = new(24.35, 54.28, 24.55, 54.50),
        ["Al Ain"] = new(24.12, 55.65, 24.28, 55.85),
        ["Al Dhafra"] = new(23.55, 53.50, 23.80, 53.90),
        ["Mussafah"] = new(24.30, 54.44, 24.38, 54.54),
        ["Sharjah"] = new(25.28, 55.35, 25.42, 55.50),
        ["Khor Fakkan"] = new(25.30, 56.30, 25.38, 56.38),
        ["Kalba"] = new(25.00, 56.30, 25.08, 56.40),
        ["Ajman"] = new(25.35, 55.45, 25.45, 55.58),
        ["Ras Al Khaimah"] = new(25.70, 55.88, 25.88, 56.02),
        ["Al Hamra"] = new(25.65, 55.75, 25.72, 55.82),
        ["Fujairah"] = new(25.08, 56.26, 25.18, 56.38),
        ["Dibba Al-Fujairah"] = new(25.56, 56.20, 25.68, 56.32),
        ["Umm Al Quwain"] = new(25.50, 55.50, 25.62, 55.62),
        ["Toronto"] = new(43.58, -79.55, 43.78, -79.25),
        ["Ottawa"] = new(45.32, -75.85, 45.48, -75.58),
        ["Mississauga"] = new(43.50, -79.75, 43.68, -79.55),
        ["Hamilton"] = new(43.18, -79.98, 43.32, -79.78),
        ["Brampton"] = new(43.65, -79.85, 43.82, -79.66),
        ["Markham"] = new(43.80, -79.42, 43.92, -79.25),
        ["Kitchener"] = new(43.40, -80.56, 43.52, -80.42),
        ["Windsor"] = new(42.25, -83.10, 42.36, -82.95),
        ["Montreal"] = new(45.42, -73.72, 45.60, -73.45),
        ["Quebec City"] = new(46.75, -71.32, 46.90, -71.12),
        ["Laval"] = new(45.54, -73.80, 45.68, -73.62),
        ["Gatineau"] = new(45.42, -75.78, 45.54, -75.62),
        ["Vancouver"] = new(49.20, -123.25, 49.32, -123.02),
        ["Victoria"] = new(48.38, -123.45, 48.50, -123.30),
        ["Surrey"] = new(49.10, -122.92, 49.25, -122.75),
        ["Burnaby"] = new(49.20, -123.05, 49.30, -122.90),
        ["Richmond"] = new(49.12, -123.20, 49.22, -123.05),
        ["Kelowna"] = new(49.82, -119.56, 49.95, -119.40),
        ["Calgary"] = new(50.92, -114.22, 51.15, -113.92),
        ["Edmonton"] = new(53.42, -113.65, 53.65, -113.35),
        ["Red Deer"] = new(52.20, -113.88, 52.34, -113.72),
        ["Lethbridge"] = new(49.64, -112.92, 49.76, -112.76),
        ["Riyadh"] = new(24.55, 46.52, 24.88, 46.85),
        ["Al Kharj"] = new(24.08, 47.22, 24.24, 47.40),
        ["Ad Diriyah"] = new(24.68, 46.52, 24.78, 46.62),
        ["Al Majma'ah"] = new(25.84, 45.26, 25.96, 45.40),
        ["Jeddah"] = new(21.40, 39.05, 21.72, 39.30),
        ["Mecca"] = new(21.30, 39.75, 21.48, 39.95),
        ["Taif"] = new(21.35, 40.40, 21.52, 40.60),
        ["Rabigh"] = new(22.72, 38.96, 22.88, 39.10),
        ["Dammam"] = new(26.32, 49.98, 26.52, 50.18),
        ["Khobar"] = new(26.12, 50.10, 26.30, 50.28),
        ["Jubail"] = new(26.92, 49.52, 27.12, 49.75),
        ["Dhahran"] = new(26.18, 49.98, 26.30, 50.10),
        ["Al Ahsa"] = new(25.30, 49.50, 25.48, 49.68),
        ["Qatif"] = new(26.50, 49.96, 26.62, 50.08),
        ["Hafar Al Batin"] = new(28.36, 45.90, 28.50, 46.05),
        ["Medina"] = new(24.42, 39.48, 24.62, 39.68),
        ["Yanbu"] = new(24.00, 37.95, 24.18, 38.15),
        ["Al Ula"] = new(26.54, 37.85, 26.70, 38.00),
        ["Abha"] = new(18.15, 42.45, 18.28, 42.58),
        ["Khamis Mushait"] = new(18.22, 42.65, 18.38, 42.82),
        ["Tabuk"] = new(28.30, 36.48, 28.48, 36.65),
        ["Sydney"] = new(-33.95, 151.05, -33.75, 151.30),
        ["Wollongong"] = new(-34.48, 150.82, -34.38, 150.96),
        ["Parramatta"] = new(-33.85, 150.95, -33.78, 151.05),
        ["Central Coast"] = new(-33.40, 151.25, -33.20, 151.50),
        ["Melbourne"] = new(-37.92, 144.85, -37.70, 145.10),
        ["Geelong"] = new(-38.20, 144.30, -38.10, 144.42),
        ["Ballarat"] = new(-37.62, 143.78, -37.50, 143.92),
        ["Bendigo"] = new(-36.82, 144.20, -36.70, 144.35),
        ["Brisbane"] = new(-27.58, 152.92, -27.35, 153.15),
        ["Gold Coast"] = new(-28.10, 153.30, -27.90, 153.50),
        ["Sunshine Coast"] = new(-26.75, 153.00, -26.55, 153.15),
        ["Townsville"] = new(-19.32, 146.74, -19.20, 146.88),
        ["Cairns"] = new(-16.98, 145.72, -16.85, 145.82),
        ["Perth"] = new(-32.05, 115.75, -31.85, 116.00),
        ["Fremantle"] = new(-32.10, 115.70, -32.02, 115.78),
        ["Mandurah"] = new(-32.58, 115.68, -32.48, 115.76),
        ["Adelaide"] = new(-35.00, 138.50, -34.85, 138.70),
        ["Canberra"] = new(-35.35, 149.05, -35.20, 149.20),
        ["Munich"] = new(48.06, 11.45, 48.22, 11.70),
        ["Nuremberg"] = new(49.38, 11.00, 49.52, 11.16),
        ["Augsburg"] = new(48.32, 10.82, 48.42, 10.98),
        ["Regensburg"] = new(48.96, 12.04, 49.08, 12.18),
        ["Wurzburg"] = new(49.74, 9.88, 49.85, 10.02),
        ["Berlin"] = new(52.42, 13.25, 52.60, 13.55),
        ["Cologne"] = new(50.88, 6.88, 51.02, 7.05),
        ["Dusseldorf"] = new(51.18, 6.70, 51.30, 6.85),
        ["Dortmund"] = new(51.45, 7.38, 51.58, 7.55),
        ["Essen"] = new(51.40, 6.94, 51.52, 7.08),
        ["Duisburg"] = new(51.38, 6.70, 51.50, 6.84),
        ["Bonn"] = new(50.68, 7.02, 50.78, 7.18),
        ["Stuttgart"] = new(48.70, 9.10, 48.85, 9.28),
        ["Karlsruhe"] = new(48.95, 8.32, 49.08, 8.48),
        ["Mannheim"] = new(49.42, 8.40, 49.55, 8.54),
        ["Heidelberg"] = new(49.35, 8.62, 49.45, 8.74),
        ["Frankfurt"] = new(50.02, 8.55, 50.18, 8.78),
        ["Wiesbaden"] = new(50.02, 8.18, 50.14, 8.30),
        ["Darmstadt"] = new(49.82, 8.58, 49.92, 8.72),
        ["Hamburg"] = new(53.45, 9.85, 53.65, 10.15),
        ["Leipzig"] = new(51.28, 12.28, 51.42, 12.45),
        ["Dresden"] = new(50.98, 13.65, 51.12, 13.82),
        ["Hanover"] = new(52.30, 9.65, 52.44, 9.82),
        ["Braunschweig"] = new(52.22, 10.45, 52.32, 10.58),
        ["Doha"] = new(25.20, 51.45, 25.38, 51.60),
        ["West Bay"] = new(25.30, 51.50, 25.35, 51.55),
        ["The Pearl"] = new(25.35, 51.52, 25.40, 51.58),
        ["Al Rayyan"] = new(25.24, 51.35, 25.34, 51.48),
        ["Education City"] = new(25.28, 51.40, 25.34, 51.46),
        ["Al Wakrah"] = new(25.12, 51.55, 25.22, 51.65),
        ["Mesaieed"] = new(24.94, 51.50, 25.04, 51.60),
        ["Al Khor"] = new(25.64, 51.45, 25.74, 51.55),
        ["Ras Laffan"] = new(25.85, 51.48, 25.96, 51.58),
        ["Lusail"] = new(25.38, 51.46, 25.46, 51.54),
        ["Muscat"] = new(23.50, 58.30, 23.65, 58.55),
        ["Seeb"] = new(23.62, 58.12, 23.72, 58.26),
        ["Ruwi"] = new(23.57, 58.52, 23.62, 58.56),
        ["Muttrah"] = new(23.60, 58.54, 23.65, 58.58),
        ["Salalah"] = new(16.95, 54.00, 17.08, 54.18),
        ["Taqah"] = new(17.00, 54.35, 17.08, 54.45),
        ["Sohar"] = new(24.28, 56.62, 24.42, 56.78),
        ["Shinas"] = new(24.70, 56.42, 24.80, 56.52),
        ["Nizwa"] = new(22.88, 57.48, 22.98, 57.58),
        ["Kuwait City"] = new(29.30, 47.90, 29.42, 48.05),
        ["Sharq"] = new(29.36, 47.97, 29.41, 48.02),
        ["Shuwaikh"] = new(29.32, 47.90, 29.38, 47.96),
        ["Hawalli"] = new(29.30, 48.00, 29.36, 48.06),
        ["Salmiya"] = new(29.30, 48.05, 29.37, 48.10),
        ["Farwaniya"] = new(29.24, 47.92, 29.32, 48.00),
        ["Al-Rai"] = new(29.28, 47.90, 29.33, 47.96),
        ["Al Ahmadi"] = new(29.02, 48.02, 29.12, 48.14),
        ["Fahaheel"] = new(29.05, 48.10, 29.12, 48.16),
        ["Manama"] = new(26.20, 50.52, 26.26, 50.62),
        ["Seef"] = new(26.22, 50.51, 26.26, 50.56),
        ["Juffair"] = new(26.20, 50.58, 26.23, 50.62),
        ["Muharraq"] = new(26.23, 50.58, 26.28, 50.64),
        ["Amwaj Islands"] = new(26.27, 50.64, 26.31, 50.68),
        ["Saar"] = new(26.17, 50.46, 26.22, 50.51),
        ["Hamad Town"] = new(26.08, 50.47, 26.15, 50.53),
        ["Riffa"] = new(26.08, 50.52, 26.16, 50.58),
        ["Kuala Lumpur"] = new(3.05, 101.60, 3.22, 101.76),
        ["Putrajaya"] = new(2.88, 101.65, 2.96, 101.73),
        ["Petaling Jaya"] = new(3.05, 101.55, 3.15, 101.65),
        ["Shah Alam"] = new(3.02, 101.46, 3.12, 101.56),
        ["Subang Jaya"] = new(3.02, 101.55, 3.09, 101.62),
        ["Cyberjaya"] = new(2.88, 101.62, 2.95, 101.68),
        ["George Town"] = new(5.36, 100.28, 5.45, 100.35),
        ["Bayan Lepas"] = new(5.25, 100.22, 5.34, 100.30),
        ["Johor Bahru"] = new(1.44, 103.68, 1.55, 103.80),
        ["Iskandar Puteri"] = new(1.38, 103.60, 1.46, 103.68),
        ["Downtown Core"] = new(1.27, 103.84, 1.30, 103.86),
        ["Orchard"] = new(1.29, 103.82, 1.32, 103.84),
        ["Marina Bay"] = new(1.27, 103.85, 1.29, 103.87),
        ["Tanjong Pagar"] = new(1.26, 103.83, 1.29, 103.85),
        ["Jurong East"] = new(1.31, 103.72, 1.35, 103.76),
        ["One-North"] = new(1.28, 103.77, 1.31, 103.80),
        ["Changi"] = new(1.33, 103.96, 1.38, 104.01),
        ["Dublin"] = new(53.28, -6.38, 53.42, -6.15),
        ["Dun Laoghaire"] = new(53.27, -6.16, 53.31, -6.10),
        ["Swords"] = new(53.42, -6.25, 53.48, -6.18),
        ["Cork"] = new(51.85, -8.55, 51.94, -8.40),
        ["Limerick"] = new(52.62, -8.68, 52.70, -8.56),
        ["Waterford"] = new(52.22, -7.16, 52.30, -7.05),
        ["Galway"] = new(53.24, -9.12, 53.30, -8.98),
        ["Sligo"] = new(54.24, -8.52, 54.30, -8.42),
        ["Amsterdam"] = new(52.30, 4.80, 52.42, 5.00),
        ["Haarlem"] = new(52.35, 4.60, 52.42, 4.68),
        ["Amstelveen"] = new(52.27, 4.83, 52.34, 4.92),
        ["Rotterdam"] = new(51.85, 4.38, 51.98, 4.58),
        ["The Hague"] = new(52.02, 4.22, 52.12, 4.38),
        ["Leiden"] = new(52.13, 4.45, 52.19, 4.54),
        ["Eindhoven"] = new(51.38, 5.40, 51.50, 5.55),
        ["Tilburg"] = new(51.52, 5.03, 51.60, 5.15),
        ["Breda"] = new(51.53, 4.71, 51.61, 4.82),
        ["Utrecht"] = new(52.04, 5.04, 52.14, 5.18),
        ["Istanbul"] = new(40.90, 28.75, 41.15, 29.25),
        ["Bursa"] = new(40.12, 28.98, 40.26, 29.18),
        ["Kocaeli"] = new(40.70, 29.85, 40.82, 30.05),
        ["Ankara"] = new(39.85, 32.72, 40.02, 33.00),
        ["Konya"] = new(37.80, 32.42, 37.94, 32.58),
        ["Izmir"] = new(38.35, 27.05, 38.52, 27.25),
        ["Denizli"] = new(37.72, 29.02, 37.84, 29.15),
        ["Bodrum"] = new(37.00, 27.38, 37.08, 27.48),
        ["Antalya"] = new(36.82, 30.60, 36.98, 30.82),
        ["Adana"] = new(36.94, 35.24, 37.06, 35.40),
        ["Zurich"] = new(47.32, 8.48, 47.43, 8.60),
        ["Winterthur"] = new(47.46, 8.68, 47.54, 8.78),
        ["Geneva"] = new(46.16, 6.08, 46.25, 6.20),
        ["Basel"] = new(47.52, 7.52, 47.60, 7.65),
        ["Lausanne"] = new(46.48, 6.56, 46.56, 6.70),
        ["Montreux"] = new(46.40, 6.88, 46.46, 6.95),
        ["Zug"] = new(47.13, 8.48, 47.20, 8.55),
        ["Bern"] = new(46.90, 7.38, 46.98, 7.50),
        ["Paris"] = new(48.78, 2.22, 48.92, 2.45),
        ["Boulogne-Billancourt"] = new(48.81, 2.22, 48.86, 2.26),
        ["Saint-Denis"] = new(48.91, 2.32, 48.96, 2.38),
        ["Lyon"] = new(45.70, 4.75, 45.82, 4.92),
        ["Grenoble"] = new(45.15, 5.68, 45.22, 5.78),
        ["Marseille"] = new(43.22, 5.30, 43.38, 5.45),
        ["Nice"] = new(43.65, 7.18, 43.76, 7.32),
        ["Cannes"] = new(43.52, 6.98, 43.58, 7.05),
        ["Toulouse"] = new(43.54, 1.36, 43.66, 1.52),
        ["Montpellier"] = new(43.56, 3.82, 43.65, 3.94),
        ["Bordeaux"] = new(44.78, -0.65, 44.90, -0.52),
        ["Lille"] = new(50.58, 2.98, 50.68, 3.12),
        ["Stockholm"] = new(59.25, 17.92, 59.40, 18.22),
        ["Kista"] = new(59.38, 17.92, 59.43, 17.98),
        ["Solna"] = new(59.34, 17.96, 59.38, 18.04),
        ["Gothenburg"] = new(57.64, 11.85, 57.78, 12.08),
        ["Boras"] = new(57.68, 12.88, 57.76, 13.00),
        ["Malmo"] = new(55.54, 12.92, 55.65, 13.08),
        ["Lund"] = new(55.67, 13.14, 55.74, 13.24),
        ["Uppsala"] = new(59.82, 17.58, 59.90, 17.70),
        ["Copenhagen"] = new(55.62, 12.48, 55.74, 12.65),
        ["Frederiksberg"] = new(55.66, 12.50, 55.70, 12.56),
        ["Lyngby"] = new(55.74, 12.46, 55.80, 12.54),
        ["Aarhus"] = new(56.10, 10.12, 56.22, 10.28),
        ["Randers"] = new(56.42, 9.98, 56.50, 10.10),
        ["Odense"] = new(55.35, 10.32, 55.45, 10.48),
        ["Esbjerg"] = new(55.42, 8.38, 55.52, 8.52),
        ["Aalborg"] = new(57.00, 9.85, 57.10, 10.00),
        ["Oslo"] = new(59.85, 10.65, 59.98, 10.88),
        ["Drammen"] = new(59.70, 10.14, 59.78, 10.28),
        ["Fredrikstad"] = new(59.18, 10.86, 59.26, 11.00),
        ["Bergen"] = new(60.32, 5.22, 60.46, 5.42),
        ["Stavanger"] = new(58.90, 5.65, 59.02, 5.82),
        ["Sandnes"] = new(58.80, 5.68, 58.90, 5.80),
        ["Trondheim"] = new(63.38, 10.30, 63.48, 10.48),
        ["Tromso"] = new(69.60, 18.88, 69.70, 19.04),
        ["Auckland"] = new(-36.95, 174.65, -36.75, 174.90),
        ["Manukau"] = new(-37.03, 174.82, -36.95, 174.94),
        ["North Shore"] = new(-36.85, 174.72, -36.75, 174.80),
        ["Wellington"] = new(-41.35, 174.70, -41.22, 174.85),
        ["Lower Hutt"] = new(-41.25, 174.87, -41.17, 174.96),
        ["Christchurch"] = new(-43.60, 172.52, -43.45, 172.75),
        ["Hamilton, NZ"] = new(-37.84, 175.22, -37.72, 175.35),
        ["Queenstown"] = new(-45.06, 168.62, -45.00, 168.72),
        ["Dunedin"] = new(-45.92, 170.45, -45.82, 170.56),
        ["Madrid"] = new(40.32, -3.82, 40.52, -3.58),
        ["Alcala de Henares"] = new(40.45, -3.40, 40.52, -3.32),
        ["Getafe"] = new(40.27, -3.77, 40.34, -3.68),
        ["Barcelona"] = new(41.32, 2.08, 41.46, 2.24),
        ["Terrassa"] = new(41.52, 1.96, 41.60, 2.06),
        ["Badalona"] = new(41.42, 2.21, 41.48, 2.28),
        ["Seville"] = new(37.32, -6.05, 37.45, -5.90),
        ["Malaga"] = new(36.68, -4.50, 36.78, -4.35),
        ["Marbella"] = new(36.48, -4.94, 36.55, -4.82),
        ["Valencia"] = new(39.40, -0.45, 39.52, -0.30),
        ["Alicante"] = new(38.30, -0.54, 38.40, -0.42),
        ["Bilbao"] = new(43.22, -2.98, 43.30, -2.88),
        ["Milan"] = new(45.38, 9.08, 45.54, 9.28),
        ["Brescia"] = new(45.50, 10.15, 45.58, 10.28),
        ["Bergamo"] = new(45.66, 9.62, 45.74, 9.74),
        ["Monza"] = new(45.55, 9.23, 45.62, 9.32),
        ["Rome"] = new(41.80, 12.35, 42.00, 12.60),
        ["Venice"] = new(45.40, 12.25, 45.48, 12.38),
        ["Verona"] = new(45.38, 10.92, 45.48, 11.06),
        ["Padua"] = new(45.36, 11.82, 45.46, 11.94),
        ["Bologna"] = new(44.44, 11.26, 44.55, 11.42),
        ["Modena"] = new(44.60, 10.86, 44.70, 10.98),
        ["Parma"] = new(44.76, 10.26, 44.85, 10.38),
        ["Turin"] = new(45.00, 7.58, 45.14, 7.78),
        ["Florence"] = new(43.72, 11.18, 43.82, 11.32),
        ["Pisa"] = new(43.68, 10.35, 43.76, 10.46),
        ["Tokyo"] = new(35.55, 139.50, 35.80, 139.85),
        ["Yokohama"] = new(35.38, 139.55, 35.52, 139.70),
        ["Kawasaki"] = new(35.48, 139.65, 35.58, 139.75),
        ["Saitama"] = new(35.80, 139.58, 35.92, 139.72),
        ["Osaka"] = new(34.60, 135.40, 34.78, 135.60),
        ["Kyoto"] = new(34.95, 135.70, 35.08, 135.85),
        ["Kobe"] = new(34.64, 135.12, 34.75, 135.28),
        ["Nagoya"] = new(35.10, 136.80, 35.25, 137.00),
        ["Hamamatsu"] = new(34.65, 137.66, 34.78, 137.80),
        ["Fukuoka"] = new(33.52, 130.30, 33.68, 130.50),
        ["Kumamoto"] = new(32.74, 130.62, 32.86, 130.78),
        ["Seoul"] = new(37.45, 126.85, 37.68, 127.15),
        ["Incheon"] = new(37.38, 126.58, 37.54, 126.80),
        ["Suwon"] = new(37.22, 126.96, 37.32, 127.08),
        ["Seongnam"] = new(37.36, 127.08, 37.46, 127.16),
        ["Busan"] = new(35.08, 128.95, 35.28, 129.20),
        ["Daegu"] = new(35.80, 128.50, 35.94, 128.70),
        ["Ulsan"] = new(35.45, 129.20, 35.62, 129.42),
        ["Daejeon"] = new(36.28, 127.30, 36.44, 127.46),
        ["Gwangju"] = new(35.10, 126.78, 35.22, 126.94),
        ["Central"] = new(22.26, 114.14, 22.30, 114.18),
        ["Wan Chai"] = new(22.26, 114.16, 22.29, 114.19),
        ["Causeway Bay"] = new(22.26, 114.17, 22.29, 114.20),
        ["Tsim Sha Tsui"] = new(22.28, 114.16, 22.32, 114.19),
        ["Mong Kok"] = new(22.30, 114.15, 22.33, 114.18),
        ["Kwun Tong"] = new(22.30, 114.21, 22.33, 114.24),
        ["Sha Tin"] = new(22.35, 114.16, 22.42, 114.22),
        ["Tsuen Wan"] = new(22.35, 114.09, 22.40, 114.14),
        ["Tallinn"] = new(59.38, 24.62, 59.48, 24.88),
        ["Maardu"] = new(59.44, 24.96, 59.50, 25.05),
        ["Viimsi"] = new(59.46, 24.80, 59.54, 24.88),
        ["Tartu"] = new(58.34, 26.65, 58.42, 26.78),
        ["Parnu"] = new(58.35, 24.44, 58.43, 24.56),
        ["Warsaw"] = new(52.12, 20.85, 52.34, 21.18),
        ["Radom"] = new(51.36, 21.08, 51.46, 21.22),
        ["Krakow"] = new(49.98, 19.82, 50.12, 20.08),
        ["Tarnow"] = new(49.98, 20.92, 50.06, 21.04),
        ["Wroclaw"] = new(51.02, 16.90, 51.18, 17.15),
        ["Legnica"] = new(51.16, 16.10, 51.25, 16.22),
        ["Katowice"] = new(50.20, 18.94, 50.32, 19.10),
        ["Gliwice"] = new(50.25, 18.60, 50.35, 18.74),
        ["Poznan"] = new(52.34, 16.82, 52.48, 17.02),
        ["Gdansk"] = new(54.28, 18.52, 54.42, 18.75),
        ["Gdynia"] = new(54.46, 18.45, 54.56, 18.58),
        ["Johannesburg"] = new(-26.30, 27.92, -26.10, 28.18),
        ["Sandton"] = new(-26.13, 28.02, -26.07, 28.08),
        ["Pretoria"] = new(-25.82, 28.12, -25.68, 28.32),
        ["Centurion"] = new(-25.90, 28.14, -25.80, 28.24),
        ["Cape Town"] = new(-34.05, 18.30, -33.80, 18.60),
        ["Stellenbosch"] = new(-33.98, 18.80, -33.88, 18.92),
        ["Durban"] = new(-29.95, 30.90, -29.75, 31.12),
        ["Umhlanga"] = new(-29.75, 31.05, -29.70, 31.12),
        ["Gqeberha"] = new(-34.02, 25.50, -33.90, 25.68),
        ["Mumbai"] = new(18.90, 72.75, 19.25, 73.05),
        ["Pune"] = new(18.42, 73.75, 18.62, 73.98),
        ["Nagpur"] = new(21.08, 79.00, 21.22, 79.18),
        ["Thane"] = new(19.16, 72.92, 19.28, 73.04),
        ["Nashik"] = new(19.92, 73.72, 20.06, 73.86),
        ["Bengaluru"] = new(12.85, 77.45, 13.10, 77.75),
        ["Mysuru"] = new(12.24, 76.58, 12.36, 76.70),
        ["Mangaluru"] = new(12.85, 74.80, 12.98, 74.92),
        ["New Delhi"] = new(28.48, 77.05, 28.75, 77.35),
        ["Gurugram"] = new(28.38, 76.95, 28.52, 77.10),
        ["Noida"] = new(28.46, 77.30, 28.62, 77.48),
        ["Chennai"] = new(12.95, 80.15, 13.20, 80.35),
        ["Coimbatore"] = new(10.95, 76.88, 11.08, 77.05),
        ["Madurai"] = new(9.88, 78.06, 9.98, 78.18),
        ["Hyderabad, India"] = new(17.28, 78.32, 17.50, 78.60),
        ["Warangal"] = new(17.92, 79.52, 18.02, 79.66),
        ["Ahmedabad"] = new(22.92, 72.45, 23.12, 72.68),
        ["Surat"] = new(21.10, 72.74, 21.25, 72.90),
        ["Vadodara"] = new(22.24, 73.10, 22.36, 73.25),
        ["Rajkot"] = new(22.24, 70.72, 22.36, 70.88),
        ["Kolkata"] = new(22.45, 22.25, 22.68, 88.48),
        ["Siliguri"] = new(26.68, 88.34, 26.78, 88.45),
        ["Jaipur"] = new(26.82, 75.68, 27.02, 75.90),
        ["Jodhpur"] = new(26.18, 72.96, 26.30, 73.10),
        ["Lucknow"] = new(26.76, 80.85, 26.94, 81.05),
        ["Kanpur"] = new(26.38, 80.24, 26.52, 80.42),
        ["Agra"] = new(27.12, 77.94, 27.24, 78.08),
        ["Kochi"] = new(9.86, 76.18, 10.02, 76.36),
        ["Thiruvananthapuram"] = new(8.45, 76.86, 8.58, 77.02)
    
    
    };

    public static readonly Dictionary<string, List<string>> CategoryPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        // Universal Full City Discovery
        ["⭐ All Commercial Businesses & Enterprises"] = ["all_commercial", "clinic", "hospital", "dentist", "pharmacy", "restaurant", "hotel", "bank", "it", "lawyer", "solar", "tailor", "factory", "industrial", "contractor", "shop"],

        // Core Sectors
        ["Healthcare & Dental"] = ["clinic", "dentist", "pharmacy", "hospital", "doctors"],
        ["Restaurants & Food"] = ["restaurant", "fast_food", "cafe"],
        ["Hotels & Lodging"] = ["hotel", "guest_house", "motel"],
        ["Retail & Shopping"] = ["supermarket", "clothes", "electronics", "department_store"],
        ["Automotive & Repair"] = ["car_repair", "car_rental", "fuel"],
        ["Education & Training"] = ["school", "college", "university"],

        // Expanded Commercial Niches
        ["Software Company & IT Services"] = ["software", "it", "computer", "saas", "tech", "developer", "web_development", "telecommunication", "office", "agency"],
        ["Software & IT Agencies"] = ["software", "it", "computer", "saas", "tech", "developer", "web_development", "telecommunication", "office", "agency"],
        ["Software Company"] = ["software", "it", "computer", "saas", "tech", "developer", "web_development", "office"],
        ["Legal & Corporate Law"] = ["lawyer", "legal", "notary", "office"],
        ["Finance & Accounting"] = ["bank", "accountant", "financial", "insurance"],
        ["Real Estate & Builders"] = ["estate_agent", "real_estate", "property_management"],
        ["Fitness Centers & Gyms"] = ["fitness_centre", "gym", "sports_centre"],
        ["Beauty Salons & Spas"] = ["hairdresser", "beauty", "spa"],
        ["Logistics & Warehousing"] = ["courier", "logistics", "storage_rental"],
        ["Manufacturing & Industrial"] = ["industrial", "factory", "warehouse"],
        ["Architecture & Construction"] = ["architect", "construction", "contractor"],
        ["Travel & Tourism Agencies"] = ["travel_agency", "tourist_guide"],
        ["Marketing & Media Studios"] = ["advertising_agency", "design", "photography"],
        ["Security & Facility Services"] = ["security", "cleaning"],

        // Additional High-Demand Commercial Niches
        ["Solar & Renewable Energy"] = ["solar", "energy", "photovoltaic", "contractor"],
        ["Event Planners & Marriage Halls"] = ["events_venue", "banquet_hall", "party", "wedding"],
        ["Printing, Packaging & Signage"] = ["copyshop", "printer", "packaging", "signage"],
        ["Agricultural & Agro-Chemical"] = ["agrarian", "fertilizer", "seed", "farm"],
        ["Pet Care & Veterinary Clinics"] = ["veterinary", "pet", "pet_grooming"],
        ["Textile, Apparel & Fashion"] = ["tailor", "fabrics", "clothes", "boutique"]
    };

    public static string BuildQuery(DiscoveryQuery query)
    {
        var timeout = Math.Max(15, Math.Min(60, query.TimeoutSeconds));
        var limit = Math.Max(10, Math.Min(500, query.Limit));

        var sb = new StringBuilder();
        sb.AppendLine($"[out:json][timeout:{timeout}];");

        // Use bounding box if provided or matched by city
        BoundingBox? bbox = query.BBox;
        if (bbox == null && !string.IsNullOrWhiteSpace(query.City) && CityBoundingBoxes.TryGetValue(query.City, out var cityBbox))
        {
            bbox = cityBbox;
        }

        List<string> amenities;
        if (query.TargetAmenities != null && query.TargetAmenities.Count > 0)
        {
            amenities = query.TargetAmenities;
        }
        else if (!string.IsNullOrWhiteSpace(query.CategoryPreset) && CategoryPresets.TryGetValue(query.CategoryPreset, out var presetList))
        {
            amenities = presetList;
        }
        else
        {
            amenities = ["clinic", "dentist", "pharmacy"];
        }

        var isAllCommercial = amenities.Any(a => a.Equals("all_commercial", StringComparison.OrdinalIgnoreCase) || a.Equals("all", StringComparison.OrdinalIgnoreCase));

        if (bbox != null)
        {
            var bboxStr = $"{bbox.South:F4},{bbox.West:F4},{bbox.North:F4},{bbox.East:F4}";
            sb.AppendLine("(");

            // Deep specific keyword search using name, shop, office, craft, amenity regex matching
            if (!string.IsNullOrWhiteSpace(query.CustomSearchTerm))
            {
                var customEscaped = query.CustomSearchTerm.Trim().Replace("\"", "\\\"");
                sb.AppendLine($"  node[\"name\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  way[\"name\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  node[\"shop\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  way[\"shop\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  node[\"office\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  way[\"office\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  node[\"craft\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  way[\"craft\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  node[\"amenity\"~\"{customEscaped}\",i]({bboxStr});");
                sb.AppendLine($"  way[\"amenity\"~\"{customEscaped}\",i]({bboxStr});");
            }

            if (isAllCommercial)
            {
                sb.AppendLine($"  node[\"amenity\"]({bboxStr});");
                sb.AppendLine($"  way[\"amenity\"]({bboxStr});");
                sb.AppendLine($"  node[\"shop\"]({bboxStr});");
                sb.AppendLine($"  way[\"shop\"]({bboxStr});");
                sb.AppendLine($"  node[\"office\"]({bboxStr});");
                sb.AppendLine($"  way[\"office\"]({bboxStr});");
                sb.AppendLine($"  node[\"craft\"]({bboxStr});");
                sb.AppendLine($"  way[\"craft\"]({bboxStr});");
                sb.AppendLine($"  node[\"healthcare\"]({bboxStr});");
                sb.AppendLine($"  way[\"healthcare\"]({bboxStr});");
                sb.AppendLine($"  node[\"industrial\"]({bboxStr});");
                sb.AppendLine($"  way[\"industrial\"]({bboxStr});");
                sb.AppendLine($"  node[\"tourism\"]({bboxStr});");
                sb.AppendLine($"  way[\"tourism\"]({bboxStr});");
            }
            else
            {
                foreach (var amenity in amenities)
                {
                    AppendAmenityClauses(sb, amenity, bboxStr);
                }
            }
            sb.AppendLine(");");
        }
        else
        {
            // Area-based query fallback
            var cityEscaped = query.City.Trim().Replace("\"", "\\\"");
            sb.AppendLine($"area[\"name\"=\"{cityEscaped}\"]->.searchArea;");
            sb.AppendLine("(");

            if (!string.IsNullOrWhiteSpace(query.CustomSearchTerm))
            {
                var customEscaped = query.CustomSearchTerm.Trim().Replace("\"", "\\\"");
                sb.AppendLine($"  node[\"name\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  way[\"name\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  node[\"shop\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  way[\"shop\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  node[\"office\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  way[\"office\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  node[\"craft\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  way[\"craft\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  node[\"amenity\"~\"{customEscaped}\",i](area.searchArea);");
                sb.AppendLine($"  way[\"amenity\"~\"{customEscaped}\",i](area.searchArea);");
            }

            if (isAllCommercial)
            {
                sb.AppendLine("  node[\"amenity\"](area.searchArea);");
                sb.AppendLine("  way[\"amenity\"](area.searchArea);");
                sb.AppendLine("  node[\"shop\"](area.searchArea);");
                sb.AppendLine("  way[\"shop\"](area.searchArea);");
                sb.AppendLine("  node[\"office\"](area.searchArea);");
                sb.AppendLine("  way[\"office\"](area.searchArea);");
                sb.AppendLine("  node[\"craft\"](area.searchArea);");
                sb.AppendLine("  way[\"craft\"](area.searchArea);");
                sb.AppendLine("  node[\"healthcare\"](area.searchArea);");
                sb.AppendLine("  way[\"healthcare\"](area.searchArea);");
                sb.AppendLine("  node[\"tourism\"](area.searchArea);");
                sb.AppendLine("  way[\"tourism\"](area.searchArea);");
            }
            else
            {
                foreach (var amenity in amenities)
                {
                    AppendAmenityClauses(sb, amenity, "area.searchArea");
                }
            }
            sb.AppendLine(");");
        }

        sb.AppendLine($"out center {limit};");
        return sb.ToString();
    }

    private static void AppendAmenityClauses(StringBuilder sb, string rawAmenity, string locationFilter)
    {
        var clean = rawAmenity.Trim().ToLowerInvariant();

        if (clean is "school" or "college" or "university" or "kindergarten" or "prep_school" or "language_school")
        {
            sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({locationFilter});");
        }
        else if (clean is "clinic" or "hospital" or "dentist" or "pharmacy" or "doctors")
        {
            sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  node[\"healthcare\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"healthcare\"=\"{clean}\"]({locationFilter});");
        }
        else if (clean is "restaurant" or "fast_food" or "cafe" or "bank" or "fuel" or "car_repair")
        {
            sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({locationFilter});");
        }
        else if (clean is "hotel" or "guest_house" or "motel")
        {
            sb.AppendLine($"  node[\"tourism\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"tourism\"=\"{clean}\"]({locationFilter});");
        }
        else if (clean is "supermarket" or "clothes" or "electronics" or "department_store" or "shop" or "convenience")
        {
            sb.AppendLine($"  node[\"shop\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"shop\"=\"{clean}\"]({locationFilter});");
        }
        else if (clean is "office" or "lawyer" or "notary" or "accountant" or "estate_agent" or "software" or "it")
        {
            sb.AppendLine($"  node[\"office\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"office\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({locationFilter});");
        }
        else if (clean is "industrial" or "factory" or "warehouse")
        {
            sb.AppendLine($"  node[\"industrial\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"industrial\"=\"{clean}\"]({locationFilter});");
        }
        else
        {
            sb.AppendLine($"  node[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"amenity\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  node[\"shop\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"shop\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  node[\"office\"=\"{clean}\"]({locationFilter});");
            sb.AppendLine($"  way[\"office\"=\"{clean}\"]({locationFilter});");
        }
    }
}
