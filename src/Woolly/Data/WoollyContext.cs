using Microsoft.EntityFrameworkCore;

public sealed class WoollyContext(DbContextOptions<WoollyContext> options) : DbContext(options)
{

}
