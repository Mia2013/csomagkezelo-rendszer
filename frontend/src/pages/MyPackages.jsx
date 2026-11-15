import { Container } from "@mui/material";
import PackageList from "../components/PackageList";

const MyPackages = () => {
  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <PackageList />
    </Container>
  );
};
export default MyPackages;
