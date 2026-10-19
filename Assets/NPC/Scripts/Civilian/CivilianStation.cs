// Which dwell clip a CivilianSpot plays. The declaration order is the threshold in the animator's Dwell
// tree (CivilianLocomotionBuilder): reorder or insert here, then rebuild the controller
public enum CivilianStation
{
    // Customers
    Shelf,
    Rack,
    Table,
    Shoes,

    // Employees
    Register,
    Bin,
    Restock,
    Tidy,
}
