import { Box, Container } from "@mui/material";
import PackageForm from "../components/PackageForm";

const CreatePackage = () => {
  return (
    <Box>
      <Container maxwidth="xl">
        <PackageForm />
      </Container>
    </Box>
  );
};
export default CreatePackage;
