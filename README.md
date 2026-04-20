# Game Asset Store

Welcome to **Game Asset Store**, a web application built using the **.NET ecosystem** designed to bridge the gap between game developers and high-quality digital assets. 

I developed this project as a hands-on deep dive into **API integration**, focusing on how to orchestrate multiple external services to create a seamless, automated workflow.

## Key Features

* **GitHub API Integration:** Instead of using a traditional file server, I implemented the GitHub API to handle asset delivery. Purchased assets are automatically managed within private GitHub repositories, ensuring version control and secure access.
* **Stripe Payment Gateway:** To handle financial transactions safely, I integrated the Stripe API. The system supports secure card processing, real-time payment validation, and automated invoice generation.
* **Asset Management:** A comprehensive catalog where users can browse, filter, and purchase 2D/3D models, scripts, and audio effects.
* **Database Management:** The application uses **Microsoft SQL Server** (hosted on MonsterASP.NET) to manage user profiles, product metadata, and transaction history.

## Learning Objectives

The primary goal of this project was to master the consumption of complex REST APIs and explore the logic of service-oriented architecture. By building this, I gained practical experience in:
* Asynchronous programming in .NET for handling external requests.
* Securely managing API keys and environment secrets.
* Implementing webhook listeners for payment confirmation.
* Automating repository management through code.

## Tech Stack

- **Backend:** .NET (ASP.NET Core MVC / Web API)
- **Database:** Microsoft SQL Server (MonsterASP.NET)
- **Payments:** Stripe API
- **Storage & Versioning:** GitHub API 
- **Frontend:** HTML5, CSS3, JavaScript (Bootstrap / Razor Pages)

---
*This project was developed as part of my personal learning journey to master API calls and backend automation.*
