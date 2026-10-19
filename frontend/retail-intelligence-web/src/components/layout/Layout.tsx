import { Outlet } from "react-router-dom";
import Sidebar from "./Slidebar";
import Header from "./Header";

export default function Layout() {
    return (
        <div className="min-h-screen bg-slate-50 flex">

            <Sidebar />

            <div className="flex-1 flex flex-col">

                <Header />

                <main className="flex-1 p-8">
                    <Outlet />
                </main>

            </div>

        </div>
    );
}