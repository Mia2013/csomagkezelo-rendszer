import { NavLink, Outlet } from "react-router";
import { allPages } from "./pages.js";
import { ROLES } from "../constants/constants.js";

const Layout = () => {
  const role = ROLES.GUEST; // később átírni AuthContextre

  const navPages = allPages.filter(
    (p) => p.showInNavbar && p.roles.includes(role),
  );

  return (
    <>
      <nav>
        {navPages.map((p) => (
          <NavLink key={p.id} to={p.path} style={{ marginRight: 12 }}>
            {p.name}
          </NavLink>
        ))}
      </nav>
      <main>
        <Outlet />
      </main>
    </>
  );
};

export default Layout;
