# ADR-002: Database per Service Pattern

## Date

2025-06-13

## Status

Accepted

## Title

Adopt Database per Service Pattern for Microservices

## Context

The Mini Order Management System is being developed using a microservices architecture. Each service (Catalog, Order, Notification) has distinct responsibilities and data domains. The project requirements specify that each service should ideally have its own database or schema, even if running on a single PostgreSQL instance. This pattern aims to promote loose coupling, independent scalability, and clear data ownership.

## Decision

We will implement the "Database per Service" pattern for the microservices. This means:

*   Each service (Catalog, Order, Notification) will manage its own dedicated database or schema.
*   Services will not directly access the databases of other services. Data sharing or communication between services will occur through well-defined APIs or asynchronous event messaging.
*   EF Core Migrations will be configured per service to manage their respective database schemas.
*   While a single PostgreSQL instance may be used, it will host separate databases (or schemas) for each service.

## Consequences

### Pros:

*   **Loose Coupling**: Services are independent and their data stores are isolated. Changes to one service's database schema do not directly impact other services.
*   **Independent Scalability**: Each service's database can be scaled independently based on its specific load and requirements.
*   **Technology Heterogeneity**: Each service can potentially use a different database technology best suited for its needs (though in this project, PostgreSQL is standardized).
*   **Clear Data Ownership**: Each service team has clear ownership and responsibility for its data.
*   **Simpler Schema Management**: Schema changes are localized to a single service's database, reducing the complexity and risk of large, coordinated schema migrations.
*   **Improved Availability**: If one service's database fails, it ideally should not bring down other unrelated services.
*   **DDD Alignment**: Aligns well with Domain-Driven Design principles, where each bounded context manages its own data.

### Cons:

*   **Distributed Transactions**: Implementing transactions that span multiple services and databases becomes significantly more complex (e.g., requiring patterns like Sagas).
*   **Data Consistency Challenges**: Maintaining data consistency across services can be challenging, often requiring eventual consistency patterns (e.g., event-driven updates).
*   **Reporting and Analytics**: Generating reports that require data from multiple services may need dedicated data aggregation strategies (e.g., data warehousing, materialized views, or API composition).
*   **Operational Overhead**: Managing multiple databases (even on a single instance) can introduce some operational overhead compared to a single monolithic database.
*   **Service-to-Service Communication**: Requires robust API design and/or event-driven mechanisms for inter-service data retrieval and consistency.

## References

*   Microservices.io - Database per Service pattern documentation.
	* https://microservices.io/patterns/data/database-per-service.html