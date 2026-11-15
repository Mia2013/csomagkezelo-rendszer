import { Container } from "@mui/material";
import PackageListUser from "../components/PackageListUser";

const MyPackages = () => {
  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <PackageListUser />
    </Container>
  );
};
export default MyPackages;
