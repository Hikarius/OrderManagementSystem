# Copilot Instructions

## Project Guidelines
- User prefers using MediatR and Unit of Work patterns in the OrderManagementSystem codebase.
- Connection strings are stored in `docker-compose.yml` and provided to services via environment variables named like `ConnectionStrings__DefaultConnection`.
- User prefers IDistributedCache for Redis caching. Implement caching with the following keys: `product:{id}` for individual products and `products:list` for the product list, using `products:list:version` for cache versioning. Invalidate the cache by removing the product key and incrementing the version on writes. After Add/Update/Delete operations, call `repository.UnitOfWork.SaveChangesAsync` to persist changes before invalidating the cache. When caching lists, ensure to update the `products:list:version` key on writes to invalidate list caches.